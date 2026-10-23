# Catalogue & playlist pagination (Spotify + YouTube + Apple)

**Cold-start reference** for how Cult Podcasts discovers recent episodes from Spotify
show catalogues, YouTube playlists, and Apple Podcasts episode lists without burning
API quota. Read this before changing paginators, expensive-query flags, `PlaylistOrder`,
or indexer skip gates.

| Companion | Purpose |
|-----------|---------|
| [youtube-playlist-order.md](youtube-playlist-order.md) | YouTube-only depth: Arbitrary playlists, known-ID probe sketch, curated failure modes |
| [indexing-app-insights-queries.md](indexing-app-insights-queries.md) | Operator KQL for circuit-breaker / flag-flip log lines |
| [post-migration/cost-analysis.md](post-migration/cost-analysis.md) | Hourly pass shaping that sets `SkipExpensive*` |

---

## 1. Why this exists

Spotify, YouTube, and Apple return paginated catalogues. Recent episodes are **not** always
on page one:

| Observed layout | Where recent episodes live | Cheap strategy |
|-----------------|----------------------------|----------------|
| Newest-first (reverse-chronological) | Head of the list | Page from offset/page 0; stop when items fall before `ReleasedSince` |
| Oldest-first (ascending) | Tail of the list | Spotify: jump to last page and walk back. YouTube/Apple: no end-jump API → capped or full walk |
| Arbitrary / curated | Either end, inconsistently | YouTube only: capped full walk + filter on the indexing-window date; never trust position |

Mis-classifying order causes either **missed episodes** (early-stop on a stale head) or
**quota burn** (unbounded walks on news-channel-scale feeds). The system therefore:

1. **Probes** order from a small lead-in sample when possible.
2. **Persists** an expensive-query flag so later passes know the cheap path (Spotify / YouTube; Apple has no expensive-query flag).
3. **Caps** walks that cannot early-stop, and **LogError**s when a cap trips so operators notice.

---

## 2. Shared concepts

### `ReleasedSince`

Indexing windows pass `IndexingContext.ReleasedSince`. Date-scoped walks must stop (or
filter) once items fall outside that window. Spotify truncates to **date-only** before
comparing (catalogue releases have no reliable time-of-day). Apple compares full
`releaseDateTime` timestamps. YouTube playlist items window on
`PlaylistItem.GetIndexingWindowDate()` — the **later** of `snippet.publishedAt`
(added-to-playlist) and `contentDetails.videoPublishedAt` (video publication). A scheduled
upload joins the playlist days before it goes public, so added-at alone drops it from the
window; a backlog video added long after publication keeps added-at, which is the
"new to this feed" signal. Playlist item requests therefore ask for `contentDetails` whenever
`ReleasedSince` is set (same 1-unit page cost).

MatchOtherServices / SubmitUrl sets `ReleasedSince` from the authority release for catalogue-floor
walks (Apple pages while the newest-first tail is still on or after that floor). A YouTube
URL with no catalogue podcast still sets that floor from the default one-hour matching
delay. Spotify episode matching does not read the floor. Name-search with no `ReleasedSince`
does not paginate the show, even when the request carries an episode release: a single-episode
match must not download the catalogue. An Apple published-before ceiling is a separate change.

### Episode lookup planner (single-episode FindEpisode, Spotify + Apple)

A single-episode lookup is planned by size and flow, and both platforms run the SAME plan through the shared
`IEpisodeLookupPlanner` / `ICatalogueLookupExecutor` in the dedicated project `RedditPodcastPoster.EpisodeLookup`
(read its [README](../Class-Libraries/RedditPodcastPoster.EpisodeLookup/README.md): it serves every use case). The platform resolvers
(`SpotifyEpisodeResolver`, `AppleEpisodeResolver`) are thin `ICatalogueCandidateSource` fetch/map adapters.

**Inputs.** `CatalogueLookup.Flow` comes from `IndexingContext.LookupFlow`, which each caller sets through `EpisodeLookupUseCases.ForX`:

| Caller | Flow |
|--------|------|
| `PodcastUpdater.Update` (Cloud indexer, Api `PodcastIndexService`, Index CLI) | `Index` |
| URL submission / `SpotifyUrlCategoriser` / `AppleUrlCategoriser` / ImageUpdater (default) | `Submit` |
| Discovery `SpotifyEnricher` / `AppleEnricher` | `Discovery` |
| `EnrichExistingEpisodesFromPodcastServices` CLI | `Enrich` |

Show size is the podcast document's per-platform count, never cross-used: `spotifyEpisodeCount` and
`appleEpisodeCount` (nullable; historic records are null). Null means unknown. The executor then makes one cheap
probe: Spotify reads page 0 (needed by most plans anyway) and takes `Paging.Total`; Apple makes one iTunes
`/lookup?id=` call (`trackCount`, `AppleCatalogueLookup`). No long query is ever made just to count.

**Strategy order** (declared in `EpisodeLookupChains`; fastest first; step 0, a known episode id, is fetched directly and a stale id falls through):

| Show size | Flow | Release | Plan |
|-----------|------|---------|------|
| no show id (for that platform) | `Index` | any | nothing: no API call; logged `plan='none' stage='skipped-no-show'` (a search can't succeed and indexing never learns a show id from it) |
| no show id | Submit / Discovery / Enrich | any | `title-search` |
| known ≤ 200 (`FullShowReadMaxEpisodes`) | `Index` | any | `full-show-read` (unbudgeted) |
| known ≤ 200 | Submit / Discovery / Enrich | any | `full-show-read` within `FullShowReadBudget` (20 s, reading + subject classification) → `title-search` (skipped when the full read completed) |
| > 200 or unknown | `Index` | any | `newest-pages` until a page is older than min(window start, release − 14 d), cap `IndexMaxPages` (20); **no search** (1 page when neither date is known) |
| > 200 or unknown | Submit / Discovery / Enrich | within `RecentWindow` (14 d) | `newest-pages` (1 page) → `title-search` → `bounded-walk` |
| > 200 or unknown | Submit / Discovery / Enrich | older or undated | `title-search` → `bounded-walk` |

- `bounded-walk`: newest pages down to the release band start (release − 14 d), capped per platform:
  `SpotifyBoundedWalkPages` = 4 pages of 50, `AppleBoundedWalkPages` = 5 pages.
  Then a clean `miss`. Oldest-first pages (Spotify "expensive" catalogues are read from the end using `Paging.Total`, so
  this is rare) disable the date stop; the cap still applies.
- `title-search`: Spotify one episode-title search (`Limit` 10, filtered to the show id or exact show name);
  Apple one iTunes `/search?entity=podcastEpisode` (limit 50) filtered to `collectionId` = the show's Apple id.
  Apple hits are hydrated through amp-api `GetEpisode` when possible.
- Pages are read once per lookup and reused across steps, ids are tried once, and pages are pass-cached
  (`SpotifyShowEpisodePages`, `AppleShowEpisodePages`; both `IPodcastPassApiCacheSource`). Failed pages are not cached.
- **Full-read budget** (non-Index): checked after each page is read and its new candidates are classified (a soft
  cap: it can overshoot by one page, e.g. ~12 s for 50 YouTube-discovered Spotify candidates). When it is
  spent the candidates already classified are still matched, then the plan moves to `title-search`, else a clean
  `miss`. Logged at Information as `Catalogue episode lookup budget spent: platform=… flow=… stage='full-show-read'
  budget-seconds=20 pages-read=…`. Real case: submission `4a7d8838` (apply 13 Oct 2026 22:57 BST) spent ~45 s per
  platform per item on The Compendium (Spotify 185 / 4 pages, Apple 159 / 16 pages), twice, and the apply outlived
  its client.
- **Shared within one operation.** Show pages (`SpotifyShowEpisodePages`, `AppleShowEpisodePages`) and candidate
  subjects (`CatalogueSubjectClassifier`, keyed by candidate title/description/release and the subject options) are
  scoped, so every lookup in one apply/submit batch or index pass reuses them. Subject classification (every subject's
  terms over each candidate, only for YouTube-discovered episodes) was the dominant cost, not the page reads.
- **Apple page size.** amp-api `/v1/catalog/us/podcasts/{id}/episodes` is called without `limit`, so Apple returns its
  default of 10 episodes per page (159 episodes = 16 pages; RE3's 38 = 4). The endpoint accepts `limit` (third-party
  reports: honoured up to 200, larger values silently capped at 200; `next` links carry the offset). Not changed here:
  a larger page changes what `AppleBoundedWalkPages` (5 pages = 50 episodes) and `IndexMaxPages` mean and must be
  verified live against amp-api first; tracked as a follow-up.
- The `newest-pages` stage does not use the incoming title, so a YouTube title that differs from the audio title
  still matches (Steve Hassan / Ben McKenzie, 12 Oct 2026: podcast `186529ba-…`, Spotify show `3fy5dEPagR2T8MPeS7ieAM`,
  Apple `1603773245`, 255 episodes).

**Timeout guard.** Every Spotify and Apple catalogue request goes through `ICatalogueRequestGuard`: a fixed
10 s per request (`RequestTimeout`, the same in every flow). When it fires it logs
`Catalogue request timeout guard fired: platform=… operation=… timeout-seconds=10` (Warning) and the request
counts as missing (a clean miss, never an exception). Caller cancellation is still propagated.

**Counts refresh.** Observed sizes are kept per scope in `ICatalogueShowEpisodeCounts`. Spotify records every
`Paging.Total` seen; Apple records only the iTunes `trackCount` or a completed full read. `PodcastUpdater`
writes them onto `spotifyEpisodeCount` / `appleEpisodeCount` when they change (no backfill job; historic
records fill in as the indexer runs).

**Logging.** One line per lookup: `Catalogue episode lookup: platform=… flow=… plan=… stage=… show-episodes=… pages-read=…`.
Stage values are `full-show-read`, `newest-pages`, `title-search`, `bounded-walk` and `miss`
(`CatalogueLookupExecutor.StageMiss`).

**Options** (`episodeLookup` section, validated at startup with `ValidateOnStart`): `FullShowReadMaxEpisodes`
(default 200, 0..1000), `FullShowReadMaxPages` (default 20, 1..100), `RecentWindow` (default 14 days, > 0), `IndexMaxPages` (default 20, 1..100),
`SpotifyBoundedWalkPages` (4) and `AppleBoundedWalkPages` (5) (1..20), `RequestTimeout` (10 s, 1..120 s).

**History.** PR #1017 (7 Oct 2026) replaced the Spotify show walk in `SpotifyEpisodeResolver` with a title search
in every flow, including the indexer's enrichment; a recent episode whose YouTube title differed from its audio
title (Ben McKenzie) then missed. The planner restores the newest-pages read for the Index flow and recent lookups
while keeping old-episode lookups on large shows bounded.

Each stage classifies subjects (`ICatalogueSubjectClassifier`, real implementation in Subjects) and
matches through `ICatalogueEpisodeMatcher`: candidates inside `EpisodeReleaseTolerance.GetSubmitMatchBand`
(release ±14 days) are tried first, others are the weaker second pass. Duration-aware matching is used
when the lookup has a length and YouTube authority or YouTube enrichment; otherwise release-date matching.

Rules: `EpisodeLookup.BusinessRules.Tests` (planner matrix, chains per use case, executor), `EpisodeLookup.Tests` (options, guard, counts),
`Episodes.Tests/BusinessRules/Matching/Candidates/*` (matcher),
`PodcastServices.CatalogueContract.Tests` (same cases and request profile through the real Spotify and Apple
resolvers, including Ben McKenzie in Index and Submit flows), `Spotify.Tests/BusinessRules/Providers/SpotifyShowEpisodePagesRules.cs`,
`Spotify.Tests/BusinessRules/Resolvers/SpotifyEpisodeResolverRules.cs`, `Apple.Tests/BusinessRules/Providers/AppleCatalogueLookupRules.cs`,
`PodcastServices.Tests/BusinessRules/Indexing/IndexingEpisodeLookupFlowRules.cs`, `Discovery.Tests/BusinessRules/Enrichers/DiscoveryLookupFlowRules.cs`.
The indexer's own episode-provider catalogue walks (sections 3 and 5) are unchanged.

The other direction uses the same band. When a Spotify or Apple submit already knows a
YouTube channel and Search.List is allowed, `YouTubeUrlCategoriser` searches that channel
with both `publishedAfter` and `publishedBefore` (`YouTubeChannelReleaseBandSearch`: at most
2 pages of 50; account-delegation forbidden throws `YouTubeChannelSearchForbiddenException`).
Expected publish is the audio release plus the podcast's YouTube publishing delay. If
`GetUploadsPlaylistReason` is set, or Search.List is account-delegation forbidden, the
categoriser does not keep calling Search.List. It reads the stored playlist when one exists,
otherwise the channel uploads playlist, sets the walk floor to the band start (a years-old
forbidden channel still pages back to that date, and the categoriser logs the date), and drops
items newer than the band end. The forbidden result is stored on
`Podcast.YouTubeChannelSearchForbidden`. No channel means no YouTube search. Indexer playlist
and uploads walks, and `GetLatestChannelVideoSnippets`, are unchanged.

### Expensive-query flags (podcast document)

| Field | Platform | Meaning when `true` |
|-------|----------|---------------------|
| `spotifyEpisodesQueryIsExpensive` | Spotify | Catalogue is oldest-first; recent episodes are near the end |
| `youTubePlaylistQueryIsExpensive` | YouTube | Playlist head is not newest-first; needs a full / expensive walk |

Both flags are **nullable**. Helpers `HasExpensiveSpotifyEpisodesQuery()` /
`HasExpensiveYouTubePlaylistQuery()` are true only when the value is explicitly `true`.

**Lifecycle (both platforms):**

1. A conclusive order probe (≥ 2 dated samples) measures newest-first vs ascending.
2. Discovery applies the measurement via `*ExpensiveQueryFlag.Apply` — **sets and clears**.
   Sticky-true alone permanently misclassifies flipped catalogues.
3. Inconclusive probes (null / sample too thin) leave the stored flag untouched.
4. Flag flips log **Warning** with a stable prefix (see §7).
5. Indexer persists the podcast when the flag changed.

### Skip gates (`IndexingContext`)

| Flag | Default | Hourly indexer allows expensive when |
|------|---------|--------------------------------------|
| `SkipExpensiveSpotifyQueries` | `true` | Primary pass **and** `hour % 6 == 0` |
| `SkipExpensiveYouTubeQueries` | `true` | Primary pass **and** `hour % 24 == 0` (midnight UTC) |

Console / API submit / discovery curation often set these to `false`.

`IndexingStrategy` owns the hour formulas; `Indexer` combines them with
`IsPrimaryPass(pass, totalPasses)`.

**Batches 2–4 do not get an expensive-allowed YouTube pass, and do not need a daily full walk from that gate.**
Expensive YouTube queries are midnight UTC only. That hour is even, so passes 1–2 run, and the primary pass is 1.
Batches 2, 3 and 4 therefore always have `SkipExpensiveYouTubeQueries` set. The hourly indexer still sets
`UseYouTubeIndexingHints` on every pass. A large oldest-first playlist with fresh hints is checked from the channel
uploads head (newest-first) on every YouTube-enabled hour for its batch — four times a day, including those
skip-expensive hours — so a video appended to the tail is found without waiting for an expensive-allowed pass.
When the last full walk is older than `FullWalkDueAfter` (24h less 1h of slack), the next YouTube-enabled pass
walks the playlist in full even though expensive queries are skipped. Playlists at or under the uploads-head
item threshold are paginated to the end on every YouTube-enabled pass; the expensive flag no longer changes that
walk.

---

## 3. Spotify catalogue pagination

### Components

| Piece | Path |
|-------|------|
| Orchestrator (probe + strategy) | `PodcastServices.Spotify/Paginators/SpotifyQueryPaginator.cs` |
| Lead-in sample | `NullEpisodesLeadInPaginator.cs` (up to 3 non-null episodes) |
| Newest-first / forward crawl | `SimpleEpisodePaginator.cs` |
| Oldest-first end-jump | `AscendingEpisodePaginator.cs` |
| Factory | `SpotifyEpisodePaginatorFactory.cs` |
| Flag apply | `Models/SpotifyExpensiveQueryFlag.cs` |
| Provider gates / page sizes | `Providers/SpotifyPodcastEpisodesProvider.cs` |
| Discovery applies flag | `SpotifyEpisodeRetrievalHandler.cs` |
| Enrichment applies flag | `Enrichers/SpotifyExpensiveQuerySideEffect.cs` |

### Decision flow

```
GetShowEpisodes (first page)
        │
        ▼
Lead-in: up to 3 non-null episodes
        │
        ▼
Probe: releases monotonically non-increasing?
        │
        ├─ yes (≥2) → ExpensiveQueryFound = false → reverse-chrono Simple walk
        ├─ no  (≥2) → ExpensiveQueryFound = true  → Ascending end-jump
        └─ <2       → ExpensiveQueryFound = null  → flag unchanged; treat as reverse for walk
        │
        ▼
ReleasedSince == null?  → known show id: PaginateAll (full catalogue; rare for indexer).
                          Name-search: refuse. Single-episode match must not walk every episode.
ReleasedSince set       → date-scoped strategy above
        │
        ▼
Final filter: release date >= ReleasedSince.Date
```

The **live probe** chooses this request's walk. The **stored**
`SpotifyEpisodesQueryIsExpensive` mainly drives first-page `Limit` and SkipExpensive gating.

### Paginators & caps

| Mode | Paginator | Cap | Stop condition |
|------|-----------|-----|----------------|
| Newest-first + `ReleasedSince` | `SimpleEpisodePaginator(isInReverseOrder: true)` | **None** | Last in-window episode falls before `ReleasedSince` |
| Oldest-first + `ReleasedSince` | `AscendingEpisodePaginator` | `MaxWalkBackPages = 5` | Page contains any out-of-window item, or walk-back cap |
| Forward fallback (missing Total/Limit) | `SimpleEpisodePaginator(isInReverseOrder: false)` | `MaxPages = 20` | Cap or catalogue end |
| No `ReleasedSince` | `PaginateAll` | Unbounded | Catalogue end |

Ascending end-jump:

1. Requires `Paging.Total` and `Limit` (> 0); else warn and fall back to capped forward walk.
2. `finalOffset = max(0, ((total - 1) / limit) * limit)` — last non-empty page.
3. Fetch that page; yield in-window items; if any item is out of window, stop (older pages are older).
4. Else follow `Previous` up to `MaxWalkBackPages`.

### Provider SkipExpensive carve-out

| SkipExpensive | HasExpensive | ReleasedSince | Effect |
|---------------|--------------|---------------|--------|
| true | true | **null** | Skip pagination — known-id: first page only; name-path: empty |
| true | true | **set** | **Still paginate** (bounded date-scoped path — safe) |
| false / not expensive | * | * | Normal pagination |

**Intentional divergence:** `SpotifyUrlCategoriser` (MatchOtherServices) skips FindEpisode
whenever expensive + SkipExpensive — **no ReleasedSince exception**. URL matching on
non-primary passes must not start catalogue walks even when discovery would.

### First-page `Limit` (when `ReleasedSince` set)

| Path | Expensive | Limit |
|------|-----------|-------|
| Known Spotify show id | true | **50** (fewer walk-back hops) |
| Known Spotify show id | false | **5** |
| Name-discovery | * | **50** |

### Business-rule tests (Spotify)

| Rule area | Test file |
|-----------|-----------|
| Ascending end-jump, walk-back cap, fallback | `Spotify.Tests/BusinessRules/Paginators/AscendingEpisodePaginatorRules.cs` |
| Simple MaxPages, reverse-chrono no-cap, null slots | `.../SimpleEpisodePaginatorRules.cs` |
| Factory selection | `.../SpotifyEpisodePaginatorFactoryRules.cs` |
| Probe / strategy / date truncate | `.../SpotifyQueryPaginatorRules.cs` |
| Lead-in null handling | `.../NullEpisodesLeadInPaginatorRules.cs` |
| Flag set / clear / inconclusive | `.../Models/SpotifyExpensiveQueryFlagRules.cs` |
| Provider Limit + SkipExpensive | `.../Providers/SpotifyPodcastEpisodesProviderRules.cs` |
| Discovery persists flag | `.../SpotifyEpisodeRetrievalHandlerRules.cs` |
| Enrichment side-effect | `.../Enrichers/SpotifyExpensiveQuerySideEffectRules.cs` |
| Categoriser hard-skip (no ReleasedSince carve-out) | `.../Categorisers/SpotifyUrlCategoriserRules.cs` |
| Single-episode title search (one page, one show, no catalogue walk) | `.../Search/SpotifyEpisodeTitleSearchRules.cs` |
| In-band match before out-of-band on that page | `.../Resolvers/SpotifyEpisodeResolverRules.cs` |
| Submit-match band width (recent and years-old) | `Episodes.Tests/BusinessRules/Matching/SubmitMatchBandRules.cs` |
| Name-search refuses a catalogue walk when `ReleasedSince` is unset, even if the episode release is set | `.../Providers/SpotifyNameMatchCatalogueLimitRules.cs` |
| Pipeline persistence | `PodcastServices.Tests/BusinessRules/Indexing/IndexingOrchestrationRules.cs` |

---

## 4. YouTube playlist pagination

### Components

| Piece | Path |
|-------|------|
| Pagination loop | `PodcastServices.YouTube/Playlist/YouTubePlaylistService.cs` |
| Head-order probe | `PlaylistItemOrdering.cs` |
| Arbitrary walk caps | `ArbitraryYouTubePlaylistWalk.cs` |
| Flag apply | `YouTubeExpensiveQueryFlag.cs` |
| Tolerant / cached wrappers | `TolerantYouTubePlaylistService.cs`, `CachedTolerantYouTubePlaylistService.cs` |
| Episode materialisation | `Episode/YouTubeEpisodeProvider.cs` |
| Discovery (applies flag) | `Handlers/YouTubeEpisodeRetrievalHandler.cs` |
| Enrichment (does **not** apply flag) | `Resolvers/YouTubeUrlResolver.cs` (`YouTubeItemResolver`) |
| Model | `Models/Podcasts/PlaylistOrder.cs`, `Podcast.YouTubePlaylistOrder`, `Podcast.YouTubePlaylistIdHistory` |

### `youTubePlaylistOrder` (`PlaylistOrder?`)

| Value | Behaviour |
|-------|-----------|
| `null` (default) | Probe head each windowed pass; maintain `youTubePlaylistQueryIsExpensive` |
| `Arbitrary` | Curated playlist; position carries no date info; capped full walk; flag untouched |
| `ReverseChronological` / `Ascending` | Reserved for future probe-written classification; **not consumed yet** |

Set manually (or via future known-ID probe). See [youtube-playlist-order.md](youtube-playlist-order.md).

### Decision flow (null order — probed)

```
playlistItems.list (every page maxResults = 50)
        │
        ▼
Sample ≥ 2 dated items among the first 3?
        │
        ├─ IsReverseDateOrdered → newest-first early-stop
        │                         IsExpensiveQuery = false
        └─ else                  → full walk
                                  IsExpensiveQuery = true
        │
        ▼
Continue while nextPageToken != null
  AND (not reverse-chrono
       OR last item's added-at is still in the ReleasedSince window
       OR that page still has an indexing-window date in the window
          and fewer than PublishedWindowExtraPages (2) such extra pages have been taken)
  AND YouTubePageWalkGuard says go on (see "Page-walk guards")
        │
        ▼
Filter results by ReleasedSince (later of added-at and video-published-at)
Discovery: YouTubeExpensiveQueryFlag.Apply (unless Arbitrary)
```

**Equal timestamps:** bulk-added items share the same added-at. Equal pairs satisfy
`IsReverseDateOrdered` (non-ascending). That mis-classifies some curated playlists as
newest-first — the main reason `Arbitrary` exists.

**Published-window extra pages.** The added-at stop above is the playlist's order, not the indexing
window (later of added-at and video-published-at). After added-at leaves the window, a newest-first
walk reads at most `YouTubePlaylistService.PublishedWindowExtraPages` (2) further pages, and only
while the page just read still has an item inside the indexing window. That is how a video added
before the window but published inside it, on page 2 or the page after, is still seen. Each extra
page is 1 quota unit. The stop is still `StoppedAtWindow` when a next token remains. Detail:
[youtube-playlist-order.md](youtube-playlist-order.md) § Newest-first paging stop.

### Arbitrary mode

- Batch size **50** (`ArbitraryYouTubePlaylistWalk.BatchSize`).
- No head-order probe; `IsExpensiveQuery` stays `null`.
- Handler never Applys the expensive flag (defense in depth even if a probe sneaks through).
- `SkipExpensiveYouTubeQueries` does **not** degrade to a single page — the capped walk is
  the only correct read.
- Circuit breaker: `MaxPages = 20` (~1000 items). Before fetching past the budget with a
  next token remaining → **LogError** and stop (prefer operator signal over quota burn).

### `RunExpensiveYouTubePlaylistPagination`

```text
HasExpensiveYouTubePlaylistQuery() && !SkipExpensiveYouTubeQueries
```

Passed as `expensivePlaylist` into the service. It no longer changes the request: every
playlist walk uses `YouTubePlaylistService.PlaylistPageSize` = 50, the YouTube maximum
(playlistItems.list is 1 unit per page whatever its size, so the old 1 / 3 / 5 / 10 sizes only
added calls).

Every non-arbitrary playlist pages on every pass (until it leaves the window when newest-first,
to the end otherwise). The handler logs this as
`playlist-paginated` (the old `playlist-single-page` label was misleading and is gone). A cheap
correct read for oldest-first playlists (uploads head + `playlistItems.list(videoId=…)`
membership checks) is a follow-up; a head-only read was tried and dropped because new uploads
sit at the tail of an oldest-first playlist and would never be found.

### Page-walk guards

YouTube can keep returning a `nextPageToken` after the last item. On some playlists (Kelly
R. Minter's, 143 items) the token points at an empty page whose own `nextPageToken` is the same
token, so a walk that only stops on a null token re-fetches that page until the function
times out. `YouTubePageWalkGuard` (playlistItems walks and the channel `search.list` walk)
stops a walk, keeps the items already gathered, and logs `YouTube page walk stopped:` with
the reason, id, source, pages fetched, `totalResults` and page size (never the token):

| Reason | When | Level |
|--------|------|-------|
| `RepeatedPageToken` | next token equals the token just requested | Warning on an empty page once ≥ `totalResults - 2` distinct ids are read (or no total), e.g. Kelly; otherwise Error |
| `EmptyPage` | a page has 0 items but a new next token, and ≥ `totalResults - 2` distinct ids are read (or no total). Short of that the walk keeps going | Information |
| `NoNewItems` | every video id on the page was already seen in this walk | Error (Warning on the channel `search.list` walk, where repeats across 5-item pages are a known cost stop) |
| `PageCapExceeded` | pages fetched reach `ceil(totalResults / 50) + 2` with a token left | Error |
| `ConsecutiveEmptyPages` | a 2nd empty page in a row (each with a new next token) while short of `totalResults - 2` distinct ids, so a chain of empty pages cannot run all the way to the cap | Error |

The cap uses `pageInfo.totalResults` and is not applied to `search.list`, whose total is an
estimate. Arbitrary-order walks keep their own `MaxPages = 20` breaker as well.

### YouTube indexing hints (uploads-head check)

An oldest-first playlist (no window stop) costs `ceil(totalResults / 50)` units every pass. For
playlists over `youTubeIndexingHints:UploadsHeadMinimumPlaylistItems` items (default **150**),
a complete full walk stores hints so the next passes can check the channel's uploads instead:

| Field | Meaning |
|-------|---------|
| `playlistId` | playlist the hints describe; hints for any other id are ignored (`CurrentYouTubeIndexingHints`) |
| `lastFullWalk` | UTC start of the last complete full walk |
| `playlistItemCount` | `pageInfo.totalResults` at the last full walk or successful check |
| `uploadsCheckedThrough` / `uploadsCheckedThroughVideoId` | newest upload (window date) already checked; the video id is informational |

Stored as `youTubeIndexingHints` on a podcast and `indexing.youTubeIndexingHints` on a TV show
or news organisation (`IYouTubeIndexingSettings`, reached through `IHasYouTubeIndexingSettings`).
Null members are omitted, so documents without hints are unchanged. Only the indexer writes them:
podcasts through `PodcastUpdater` (a hints-only change still saves), parents through the
`/indexing/youTubeIndexingHints` patch. Curator saves load and save the stored hints unchanged.

`YouTubeIndexingHintsPlanner` picks **uploads-head** only when the caller opted in
(`IndexingContext.UseYouTubeIndexingHints`), the hints match the playlist, `playlistItemCount` >
threshold, `lastFullWalk` is younger than `FullWalkInterval` less `FullWalkSlack` (defaults **24h**
and **1h**, so the daily walk is due after 23h and start-time jitter cannot slip it a pass) and there
is a `ReleasedSince` window. Otherwise it runs the normal full walk (`not-opted-in`, `skip-youtube`,
`playlist-changed`, `full-walk-due`, `no-window`, …).

**Opt-in:** only the hourly Cloud indexer (`Cloud/Indexer/Activities/Indexer.cs`) sets
`UseYouTubeIndexingHints`. Api "index podcast" (curator re-index) and the Index console app
(`--released-since` backfills) always walk in full, so they recover an in-window episode the uploads
check would never offer again (an earlier failed pass, or a window wider than the watermark). Their
complete walks still refresh the hints.
The uploads-head check is **not** gated on `SkipExpensiveYouTubeQueries`. Batches 2–4, which never
get an expensive-allowed pass (see §2), still run it on every YouTube-enabled hour, so a new item
appended to the tail of an oldest-first playlist is discovered for a podcast in any batch. The
age-based full walk (`full-walk-due`) also runs on a skip-expensive pass.

`YouTubeUploadsHeadCheck` then:

1. reads the playlist head (`maxResults=1`, 1 unit) for `totalResults`;
2. reads the newest 50 channel uploads (`UU…`, 1 unit) and keeps those newer than the watermark
   (later of `lastFullWalk` / `uploadsCheckedThrough`, strict `>`);
3. `delta = total - playlistItemCount`. 0 → nothing new (the watermark moves on). Otherwise each
   newer upload is checked with `playlistItems.list?videoId=` (1 unit each), and the members found
   must equal `delta`.

Any doubt falls back to the full walk, which rewrites the hints: `no-uploads-playlist`,
`playlist-count-failed`, `uploads-read-failed`, `uploads-gap` (a full page of 50 with no dated
upload at or before the watermark; undated uploads prove nothing),
`playlist-count-changed` (removals, or members ≠ delta), `full-walk-cheaper`, `membership-unknown`,
`build-failed`. Quota exhausted stops with no full walk. Hints are cleared (null) when a complete
walk finds ≤ threshold items or a walk stopped at the window (newest-first); an incomplete walk
(Error guard stop, circuit breaker, failure, failed build) leaves them as they were. A pass where a
found episode failed to merge keeps the previous hints (`PodcastUpdater`, `CatalogueParentsUpdater`;
logged `YouTubeIndexingHints kept previous hints`), so the next pass offers that episode again; an
episode save failure aborts the pass before anything is saved. A net-zero add + remove is
picked up by the daily full walk. Log prefix `YouTubeIndexingHints`; path log
`playlist-uploads-head`.

### Known-id skip on playlist discovery

Discovery passes the YouTube ids of the stored episodes it already read (the repository scopes
them to the release window) as `knownIds` (`YouTubeEpisodeProvider.GetPlaylistEpisodes`), so
known videos skip `videos.list`, the thumbnail probe and episode building — the same rule as
channel discovery. The set is **not** re-filtered on `ReleaseUtc`, because playlist items are
windowed on the later of added-at and video-published-at (an old video newly added to the
playlist stays known). A stored episode that merge would still improve is left out of the set
and re-fetched as before: no YouTube url, no YouTube image or no description
(`EpisodePlatformMerger` → `ApplyFillMissing(ToYouTubePatch())`), or a date-only (midnight)
release (`YouTubeTimeBackfillMergePolicy`). Enrichment does not fill these on an episode that
already has its YouTube id. `YouTubeDiscoveryPath` logs `known-skipped` next to `episodes-found`
(which counts new videos only; in-window = `episodes-found + known-skipped`).

### Discovery vs enrichment

| Concern | Discovery | Enrichment |
|---------|-----------|------------|
| Entry | `YouTubeEpisodeRetrievalHandler` | `YouTubeItemResolver` |
| Forwards `YouTubePlaylistOrder` | Yes | Yes |
| Applies expensive flag | **Yes** (unless Arbitrary) | **No** |
| Path log | `YouTubeDiscoveryPath` (`playlist-arbitrary-full-walk` / `playlist-paginated` / `channel`; `playlist-paginated` replaced the misleading `playlist-single-page`; `known-skipped` added) | Match via `PlaylistItemFinder` |

### YouTube API cost reminder

`playlistItems.list` = **1 quota unit per page**. `search.list` = **100**. A capped
Arbitrary walk of 20×50 items costs ≤ 20 units — far cheaper than one channel search, but
still must be capped so a mis-tagged uploads feed cannot empty the daily key budget.

### Business-rule tests (YouTube)

| Rule area | Test file |
|-----------|-----------|
| Arbitrary MaxPages / message prefix / item budget | `YouTube.Tests/Playlist/ArbitraryYouTubePlaylistWalkRules.cs` |
| Head-order probe (incl. equal timestamps) | `YouTube.Tests/Playlist/PlaylistItemOrderingRules.cs` |
| Flag Apply set / clear / inconclusive | `YouTube.Tests/Playlist/YouTubeExpensiveQueryFlagRules.cs` |
| Discovery flag round-trip + Arbitrary | `YouTube.Tests/Handlers/YouTubeEpisodeRetrievalHandlerRules.cs` |
| Scheduled-upload indexing window (later of added-at / video-published-at) | `YouTube.Tests/Extensions/ScheduledUploadIndexingWindowRules.cs`, `YouTube.Tests/Episode/YouTubeEpisodeProviderScheduledUploadRules.cs` |
| Playlist discovery skips known video ids (window-aligned; incomplete stored episodes stay re-fetched); path labels | `YouTube.Tests/Episode/YouTubeEpisodeProviderKnownIdRules.cs`, `YouTube.Tests/Handlers/YouTubeEpisodeRetrievalHandlerPlaylistCostRules.cs` |
| Discovery path wiring | `YouTube.Tests/Handlers/YouTubeEpisodeRetrievalHandlerTests.cs` |
| `RunExpensiveYouTubePlaylistPagination` | `YouTube.Tests/IndexingContextExtensionsTests.cs` |
| Known-channel submit searches the release band; forbidden and PreferUploadsPlaylist use a band-bounded playlist | `YouTube.Tests/BusinessRules/Services/YouTubeSubmitMatchBandRules.cs` |
| Channel search is at most two pages and throws when Search.List is account-delegation forbidden | `YouTube.Tests/BusinessRules/ChannelSnippets/YouTubeChannelReleaseBandSearchRules.cs` |
| Pipeline persistence | `PodcastServices.Tests/BusinessRules/Indexing/IndexingOrchestrationRules.cs` |
| Page-walk guards incl. two consecutive empty pages | `YouTube.Tests/Paging/YouTubePlaylistServicePagingRules.cs`, `YouTube.Tests/BusinessRules/Paging/YouTubeConsecutiveEmptyPagesRules.cs` |
| Newest-first extra pages for a video added outside the window but published inside it, and the extra-page quota bound | `YouTube.Tests/BusinessRules/Paging/YouTubePublishedWindowPagingRules.cs` |
| Tail append on an oldest-first playlist is found from the uploads head when expensive queries are skipped (any batch) | `YouTube.Tests/BusinessRules/IndexingHints/YouTubeUploadsHeadCheckRules.cs`, `Indexer.Tests/BusinessRules/YouTubeBatchUploadsHeadCoverageRules.cs` |
| Indexing hints: planner (opt-in, skip-YouTube, slack), uploads-head check (quota per path, strict `>` watermark, count drop, full-walk-cheaper, undated gap, window/known filters), walk outcome (circuit breaker, failed build), handler | `YouTube.Tests/BusinessRules/IndexingHints/*.cs`, `YouTube.Tests/Episode/YouTubeEpisodeProviderBuildPlaylistEpisodesRules.cs` |
| Indexing hints: only the hourly indexer opts in; curator re-index does not | `Indexer.Tests/IndexerActivityParentFailureRules.cs`, `FunctionHost.Tests/Api/Services/Podcasts/PodcastIndexServiceYouTubeIndexingHintsRules.cs` |
| Indexing hints: storage, patch, hints-only save, failed merge keeps previous hints, curator saves keep them | `Persistence.Tests/BusinessRules/YouTubeIndexingHintsPersistenceRules.cs`, `PodcastServices.Tests/BusinessRules/Indexing/YouTubeIndexingHintsSaveRules.cs`, `CatalogueParentIndexingRules.cs`, `FunctionHost.Tests/Api/Services/Catalogue/CuratorSaveKeepsYouTubeIndexingHintsRules.cs` |

---

## 5. Apple catalogue pagination

### Components

| Piece | Path |
|-------|------|
| HTTP paging + order probe | `PodcastServices.Apple/Providers/ApplePodcastService.cs` |
| Probe / continue / MaxPages rules | `Providers/AppleCataloguePagination.cs` |
| Per-pass cache | `Providers/CachedApplePodcastService.cs` |
| Indexer filter after fetch | `Providers/AppleEpisodeProvider.cs` |
| MatchOtherServices resolve | `Categorisers/AppleUrlCategoriser.cs` → `Resolvers/AppleEpisodeResolver.cs` |

Apple has **no** expensive-query podcast flag and **no** SkipExpensiveApple gate. MatchOtherServices
relies on `ReleasedSince` early-stop (and MaxPages when the head is not newest-first).

### Decision flow

```
GET /v1/catalog/us/podcasts/{id}/episodes (page 1)
        │
        ▼
Probe first page: releases monotonically non-increasing?
  (equal timestamps count as newest-first — same as Spotify/YouTube)
        │
        ├─ newest-first + ReleasedSince → follow next while last collected >= ReleasedSince
        ├─ not newest-first + ReleasedSince → follow next up to MaxPages = 20, then LogError
        └─ ReleasedSince null             → full catalogue until next empty
        │
        ▼
FindEpisode / indexer filter on collected slice
```

**Equal timestamps:** same-day batching used to fail a strict `>` probe and disable
ReleasedSince early-stop — walking entire high-volume shows on SubmitUrl `-m`. The probe
now treats non-increasing sequences as newest-first.

**Legacy `breakEvaluator`:** private Apple hook meant to stop when a specific catalogue
record matched (originally find-by-id before the episode endpoint existed). Callers always
pass `null` today. It is **not** the cross-service early-stop pattern; date-scoped walks are.

### Caps

| Mode | Cap | Stop condition |
|------|-----|----------------|
| Newest-first + `ReleasedSince` | **None** | Last collected release falls before `ReleasedSince` |
| Not newest-first + `ReleasedSince` | `MaxPages = 20` subsequent fetches | Cap or catalogue end; Error log when cap trips with `next` remaining |
| No `ReleasedSince` | Unbounded | Catalogue end |

### Business-rule tests (Apple)

| Rule area | Test file |
|-----------|-----------|
| Equal-date newest-first; thin sample; ReleasedSince continue / boundary; MaxPages | `Apple.Tests/BusinessRules/Providers/AppleCataloguePaginationRules.cs` |
| GetEpisodes equal-date early-stop; in-window follow; SubmitUrl thousands-episode head-only; ascending MaxPages; null-window full walk | `.../ApplePodcastServicePaginationRules.cs` |
| MatchOtherServices YouTube/Spotify → Apple ReleasedSince window | `UrlSubmission.Tests/BusinessRules/UrlSubmission/UrlCategoriserReleasedSinceRules.cs` |

---

## 6. Side-by-side comparison

| Concern | Spotify | YouTube | Apple |
|---------|---------|---------|-------|
| Newest-first strategy | Reverse-chrono Simple; stop on `ReleasedSince` | Early-stop on added-at, then at most 2 extra pages while an item is still inside the indexing window | Early-stop while last collected >= `ReleasedSince` |
| Oldest-first strategy | End-jump + walk back (`MaxWalkBackPages=5`) | Full forward walk (no API end-jump) | Forward walk capped at `MaxPages=20` when window set |
| Arbitrary / curated | N/A (catalogue is API-ordered) | `PlaylistOrder.Arbitrary` + capped walk | N/A |
| Forward / unordered cap | `SimpleEpisodePaginator.MaxPages=20` | `ArbitraryYouTubePlaylistWalk.MaxPages=20` | `AppleCataloguePagination.MaxPages=20` |
| Order probe sample | Lead-in ≤ 3 episodes | First page sample (≥ 2 dated) | First page sample (equal dates = newest-first) |
| Expensive-query flag | Yes | Yes | **No** |
| Flag cleared on flip to newest-first | Yes | Yes | N/A |
| Expensive allowed (hourly) | `hour % 6 == 0` + primary pass | `hour % 24 == 0` + primary pass | N/A |
| SkipExpensive + ReleasedSince still walks | Provider: **yes** | Arbitrary: always; ascending known-expensive: still may full-walk on live probe | N/A |
| Circuit-breaker log level | **Error** | **Error** (Arbitrary) | **Error** (unordered + window) |
| Flag-flip log level | **Warning** | **Warning** | N/A |

---

## 7. Operator playbook

| Symptom | Likely cause | Check |
|---------|--------------|-------|
| Recent episode missing Spotify URL | Ascending catalogue + skip-expensive without window; or walk-back circuit breaker | Flag flip / circuit-breaker Error; `ReleasedSince` window |
| Recent YouTube URL missing on curated show | Playlist not `Arbitrary`, or wrong playlist id, or Arbitrary cap tripped | `youTubePlaylistOrder`, playlist id / `youTubePlaylistIdHistory`, Arbitrary circuit-breaker Error |
| YouTube episode public today but not indexed, and its playlist item was added days ago | Scheduled upload — added-at is outside the window, video-published-at is inside | `contentDetails.videoPublishedAt` vs `snippet.publishedAt` on the playlist item |
| SubmitUrl `-m` pages entire Apple catalogue | Broken newest-first probe (fixed: equal dates) or missing `ReleasedSince`; or unordered head hit MaxPages | Apple circuit-breaker Error; MatchOtherServices ReleasedSince |
| Expensive flag stuck `true` forever | Probe never returned conclusive newest-first (should not happen — Apply clears) | Flag-flip Warning history |
| Quota spike on YouTube | Mis-tagged Arbitrary on a huge playlist; or many ascending full walks | Arbitrary circuit-breaker; hour-0 expensive YouTube pass; `YouTubeIndexingHints` fallback reasons |

---

## 8. Stable log prefixes (App Insights)

| Prefix | Level | Source |
|--------|-------|--------|
| `Spotify pagination circuit-breaker tripped:` | Error | `SimpleEpisodePaginator`, `AscendingEpisodePaginator` |
| `Spotify expensive-query flag flipped:` | Warning | `SpotifyExpensiveQueryFlag` |
| `YouTube expensive-query flag flipped:` | Warning | `YouTubeExpensiveQueryFlag` |
| `YouTube playlist id changed:` | Warning | `YouTubePlaylistIdChange` (API / URL-submit playlist swaps; former id kept on `youTubePlaylistIdHistory`) |
| `YouTube arbitrary-playlist walk circuit-breaker tripped:` | Error | `YouTubePlaylistService` via `ArbitraryYouTubePlaylistWalk` |
| `YouTubeDiscoveryPath` | Info / Warning | `YouTubeEpisodeRetrievalHandler` |
| `YouTubeIndexingHints` | Info | `YouTubeEpisodeRetrievalHandler` (uploads-head result, fallback reason, hints written / cleared); `PodcastUpdater` / `CatalogueParentsUpdater` (previous hints kept after a failed merge) |
| `Apple pagination circuit-breaker tripped:` | Error | `ApplePodcastService` via `AppleCataloguePagination` |
| `Catalogue episode lookup:` | Info | `CatalogueLookupExecutor` (one line per single-episode lookup) |
| `Catalogue request timeout guard fired:` | Warning | `CatalogueRequestGuard` (Spotify + Apple, 10 s) |

KQL: [indexing-app-insights-queries.md](indexing-app-insights-queries.md) (§11 for the episode lookup lines).

Function apps cap `RedditPodcastPoster` at Warning. The Info `Catalogue episode lookup:` line reaches App Insights only through the `RedditPodcastPoster.EpisodeLookup` = Information override in `Cloud/Azure/ApplicationLogLevels.cs` and each function app's `host.json` (pinned by `FunctionHost.Tests` `EpisodeLookupLogLevelRules`). Keep it when changing log levels.

---

## 9. Agent / contributor checklist

Before changing pagination, order probes, flags, or `PlaylistOrder`:

- [ ] Re-read this doc and the platform companion (YouTube: [youtube-playlist-order.md](youtube-playlist-order.md))
- [ ] Update or add a `BusinessRules/**` test with `DisplayName` stating the rule (see `.cursor/rules/unit-tests.mdc`)
- [ ] Keep circuit-breaker / flag-flip **message prefixes** stable — App Insights alerts key off them
- [ ] Do not invent a third Spotify order mode without an end-jump or hard cap
- [ ] Do not remove Arbitrary's `MaxPages` cap (or Apple's unordered `MaxPages` when `ReleasedSince` is set)
- [ ] Do not make expensive-query flags sticky-true-only
- [ ] Keep single-episode lookups on the shared planner: no platform-specific strategy, every page read bounded
- [ ] Do not treat equal Apple release timestamps as “not newest-first”
- [ ] If changing hourly gates, update `IndexingStrategy` tests / cost-analysis notes
- [ ] Run `dotnet test` on affected `*Tests` projects + `pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged`
