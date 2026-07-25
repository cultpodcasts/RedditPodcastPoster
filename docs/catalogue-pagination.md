# Catalogue & playlist pagination (Spotify + YouTube)

**Cold-start reference** for how Cult Podcasts discovers recent episodes from Spotify
show catalogues and YouTube playlists without burning API quota. Read this before
changing paginators, expensive-query flags, `PlaylistOrder`, or indexer skip gates.

| Companion | Purpose |
|-----------|---------|
| [youtube-playlist-order.md](youtube-playlist-order.md) | YouTube-only depth: Arbitrary playlists, known-ID probe sketch, curated failure modes |
| [indexing-app-insights-queries.md](indexing-app-insights-queries.md) | Operator KQL for circuit-breaker / flag-flip log lines |
| [post-migration/cost-analysis.md](post-migration/cost-analysis.md) | Hourly pass shaping that sets `SkipExpensive*` |

---

## 1. Why this exists

Both Spotify and YouTube return paginated catalogues. Recent episodes are **not** always
on page one:

| Observed layout | Where recent episodes live | Cheap strategy |
|-----------------|----------------------------|----------------|
| Newest-first (reverse-chronological) | Head of the list | Page from offset/page 0; stop when items fall before `ReleasedSince` |
| Oldest-first (ascending) | Tail of the list | Spotify: jump to last page and walk back. YouTube: no end-jump API → full walk (expensive) |
| Arbitrary / curated | Either end, inconsistently | YouTube only: capped full walk + filter on added-at; never trust position |

Mis-classifying order causes either **missed episodes** (early-stop on a stale head) or
**quota burn** (unbounded walks on news-channel-scale feeds). The system therefore:

1. **Probes** order from a small lead-in sample when possible.
2. **Persists** an expensive-query flag so later passes know the cheap path.
3. **Caps** walks that cannot early-stop, and **LogError**s when a cap trips so operators notice.

---

## 2. Shared concepts

### `ReleasedSince`

Indexing windows pass `IndexingContext.ReleasedSince`. Date-scoped walks must stop (or
filter) once items fall outside that window. Spotify truncates to **date-only** before
comparing (catalogue releases have no reliable time-of-day). YouTube playlist items use
`snippet.publishedAt` = **added-to-playlist** time for playlist walks.

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
ReleasedSince == null?  → PaginateAll (full catalogue; rare for indexer)
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
| Model | `Models/Podcasts/PlaylistOrder.cs`, `Podcast.YouTubePlaylistOrder` |

### `youTubePlaylistOrder` (`PlaylistOrder?`)

| Value | Behaviour |
|-------|-----------|
| `null` (default) | Probe head each windowed pass; maintain `youTubePlaylistQueryIsExpensive` |
| `Arbitrary` | Curated playlist; position carries no date info; capped full walk; flag untouched |
| `ReverseChronological` / `Ascending` | Reserved for future probe-written classification; **not consumed yet** |

Set manually (or via future known-ID probe). See [youtube-playlist-order.md](youtube-playlist-order.md).

### Decision flow (null order — probed)

```
playlistItems.list (small first page)
        │
        ▼
Sample ≥ 2 dated items?
        │
        ├─ IsReverseDateOrdered → newest-first early-stop (batch 1 or 10)
        │                         IsExpensiveQuery = false
        └─ else                  → ascending full walk (batch 10)
                                  IsExpensiveQuery = true
        │
        ▼
Continue while nextPageToken != null
  AND (not reverse-chrono OR last item still in ReleasedSince window)
        │
        ▼
Filter results by ReleasedSince (added-at)
Discovery: YouTubeExpensiveQueryFlag.Apply (unless Arbitrary)
```

**Equal timestamps:** bulk-added items share the same added-at. Equal pairs satisfy
`IsReverseDateOrdered` (non-ascending). That mis-classifies some curated playlists as
newest-first — the main reason `Arbitrary` exists.

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

Passed as `expensivePlaylist` into the service. Affects **batch size after a reverse-chrono
probe** (1 vs 10). If the live head probe says ascending, the service still walks until
`nextPageToken` is null even when `expensivePlaylist` is false — the handler's
`"playlist-single-page"` label means “this pass did not request expensive batch sizing,”
not a hard one-page stop inside `YouTubePlaylistService`.

### Discovery vs enrichment

| Concern | Discovery | Enrichment |
|---------|-----------|------------|
| Entry | `YouTubeEpisodeRetrievalHandler` | `YouTubeItemResolver` |
| Forwards `YouTubePlaylistOrder` | Yes | Yes |
| Applies expensive flag | **Yes** (unless Arbitrary) | **No** |
| Path log | `YouTubeDiscoveryPath` (`playlist-arbitrary-full-walk` / `playlist-paginated` / `playlist-single-page` / `channel`) | Match via `PlaylistItemFinder` |

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
| Discovery path wiring | `YouTube.Tests/Handlers/YouTubeEpisodeRetrievalHandlerTests.cs` |
| `RunExpensiveYouTubePlaylistPagination` | `YouTube.Tests/IndexingContextExtensionsTests.cs` |
| Pipeline persistence | `PodcastServices.Tests/BusinessRules/Indexing/IndexingOrchestrationRules.cs` |

---

## 5. Side-by-side comparison

| Concern | Spotify | YouTube |
|---------|---------|---------|
| Newest-first strategy | Reverse-chrono Simple; stop on `ReleasedSince` | Early-stop while last added-at in window |
| Oldest-first strategy | End-jump + walk back (`MaxWalkBackPages=5`) | Full forward walk (no API end-jump) |
| Arbitrary / curated | N/A (catalogue is API-ordered) | `PlaylistOrder.Arbitrary` + capped walk |
| Forward / unordered cap | `SimpleEpisodePaginator.MaxPages=20` | `ArbitraryYouTubePlaylistWalk.MaxPages=20` |
| Order probe sample | Lead-in ≤ 3 episodes | First page sample (≥ 2 dated) |
| Flag cleared on flip to newest-first | Yes | Yes |
| Expensive allowed (hourly) | `hour % 6 == 0` + primary pass | `hour % 24 == 0` + primary pass |
| SkipExpensive + ReleasedSince still walks | Provider: **yes** | Arbitrary: always; ascending known-expensive: still may full-walk on live probe |
| Circuit-breaker log level | **Error** | **Error** (Arbitrary) |
| Flag-flip log level | **Warning** | **Warning** |

---

## 6. Operator playbook

| Symptom | Likely cause | Check |
|---------|--------------|-------|
| Recent episode missing Spotify URL | Ascending catalogue + skip-expensive without window; or walk-back circuit breaker | Flag flip / circuit-breaker Error; `ReleasedSince` window |
| Recent YouTube URL missing on curated show | Playlist not `Arbitrary`, or wrong playlist id, or Arbitrary cap tripped | `youTubePlaylistOrder`, playlist id, Arbitrary circuit-breaker Error |
| Expensive flag stuck `true` forever | Probe never returned conclusive newest-first (should not happen — Apply clears) | Flag-flip Warning history |
| Quota spike on YouTube | Mis-tagged Arbitrary on a huge playlist; or many ascending full walks | Arbitrary circuit-breaker; hour-0 expensive YouTube pass |

---

## 7. Stable log prefixes (App Insights)

| Prefix | Level | Source |
|--------|-------|--------|
| `Spotify pagination circuit-breaker tripped:` | Error | `SimpleEpisodePaginator`, `AscendingEpisodePaginator` |
| `Spotify expensive-query flag flipped:` | Warning | `SpotifyExpensiveQueryFlag` |
| `YouTube expensive-query flag flipped:` | Warning | `YouTubeExpensiveQueryFlag` |
| `YouTube arbitrary-playlist walk circuit-breaker tripped:` | Error | `YouTubePlaylistService` via `ArbitraryYouTubePlaylistWalk` |
| `YouTubeDiscoveryPath` | Info / Warning | `YouTubeEpisodeRetrievalHandler` |

KQL: [indexing-app-insights-queries.md](indexing-app-insights-queries.md).

---

## 8. Agent / contributor checklist

Before changing pagination, order probes, flags, or `PlaylistOrder`:

- [ ] Re-read this doc and the platform companion (YouTube: [youtube-playlist-order.md](youtube-playlist-order.md))
- [ ] Update or add a `BusinessRules/**` test with `DisplayName` stating the rule (see `.cursor/rules/unit-tests.mdc`)
- [ ] Keep circuit-breaker / flag-flip **message prefixes** stable — App Insights alerts key off them
- [ ] Do not invent a third Spotify order mode without an end-jump or hard cap
- [ ] Do not remove Arbitrary's `MaxPages` cap
- [ ] Do not make expensive-query flags sticky-true-only
- [ ] If changing hourly gates, update `IndexingStrategy` tests / cost-analysis notes
- [ ] Run `dotnet test` on affected `*Tests` projects + `pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged`
