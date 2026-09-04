# Handoff: Continue streaming-provider scrapers (Cult Podcasts)

You are continuing **streaming / non-podcast URL submit scrapers** for Cult Podcasts. Primary repo is **RedditPodcastPoster (RPP)**. Do **not** implement the catalogue-content-types epic. Do **not** merge PRs or deploy unless the user explicitly asks in this conversation.

Prior session transcript (context only): [`f40da660-f9c4-4c8c-87f6-2e040beead0b`](f40da660-f9c4-4c8c-87f6-2e040beead0b)

---

## 1. Goal

Add **new streaming-provider plugins** (matcher + page/oEmbed extractor + DI + `NonPodcastService` + catalog key + live/canonical tests) so users can:

1. `GET /submit/lookup` an unknown URL → `kind: streaming` + best-effort **`podcastName`** (series brand), or `null` for films/one-offs.
2. `POST /submit` with `{ url, podcastName? }` → attach via name lookup or create Podcast + Episode under today’s Podcast/Episode model.
3. Never treat the **platform publisher** (e.g. “Netflix”, “ITVX”) as the series name.

Ship as one or more RPP PRs. Touch **Api / website** only if you add new `ServiceCatalog` keys that need icon/host parity in `service-catalog.ts`, or lookup/submit contract changes.

---

## 2. Done already (PR 966 — merged)

**PR:** https://github.com/cultpodcasts/RedditPodcastPoster/pull/966 — *Service catalog, streaming submit, and name-conflict 409* (merged 2026-09-03).

### Shipped

| Area | What |
|------|------|
| **Service catalog** | Ordered keys in `ServiceCatalog` / website `service-catalog.ts`: YouTube, Spotify, Apple, BBC iPlayer/Sounds, Internet Archive, Vimeo, Netflix, Amazon Prime, **Paramount+**, **HBO Max**, **Play Suisse**, **TVNZ+** |
| **Extractors (submit)** | BBC Sounds, BBC iPlayer, Internet Archive, **Vimeo**, **Netflix**, **Amazon Prime** |
| **Submit gating** | `INonPodcastServiceAdapterResolver.ForSubmit(url)` / `ForExtract(url)` — path-strict where needed (BBC Sounds/iPlayer; IA `/details`) |
| **Name conflict** | Name-only `POST /submit`: unique → attach; **many → 409** UUID list; none → create series. Missing `podcastId` → **404** (not create-another). Lookup ambiguity is **200 + podcastIds**, not 409 |
| **Lookup** | Read-only `GET` SubmitUrl / Worker `GET /submit/lookup` — URL membership; unknown streaming may return scraped `podcastName` |
| **Show-name rules** | `NonPodcastShowNameResolver` |

### `NonPodcastShowNameResolver` (do not break)

    ShowName present + (non-Vimeo) ShowName == Publisher  → null  (platform brand ≠ series)
    ShowName present + distinct from publisher            → ShowName
    No ShowName + Vimeo                                   → Publisher (author/channel)
    No ShowName + other                                   → null
    Films / one-offs                                      → extractors set ShowName null → lookup podcastName null

Canonical live expectations: films → `ExpectedPodcastName: null`; series → brand title; Vimeo → author (see `StreamingScraperCanonicalCases`).

### Live test gates

- CI sets `SKIP_LIVE_STREAMING_SCRAPER_TESTS=1` (`.github/workflows/dotnet.yml` / deploy test job) so Build stays mocked.
- Local / nightly: unset or not `1` to run live HTTP Theories.
- **Canonical cases:** `Class-Libraries/RedditPodcastPoster.UrlSubmission.Tests/Support/StreamingScraperCanonicalCases.cs`
- **Browse harvest:** `StreamingScraperBrowsePages.cs` + `StreamingScraperBrowsePageHarvestRules`
- Gate helper: `StreamingScraperLiveTestGate.cs`
- Probe: `StreamingScraperCanonicalUrlProbe.cs` → `NonPodcastShowNameResolver.TrySeriesName`

### Sibling release (already coordinated with 966 — **merged**)

- Website PR 483 (Series typeahead + lookup UX) — **merged**
- Api Worker PR 142 (`POST /submit` 400/404/409; `GET /submit/lookup`) — **merged**

Submit UX docs: `website/cultpodcasts/docs/submit-url-flows.md`

---

## 3. Target providers

**Priority = user-named next wave.** Catalog-only rows already have keys/hosts but **no** extractors / `NonPodcastService` enum values.

| Provider | Hosts / notes | Catalog today? | Work needed |
|----------|---------------|----------------|-------------|
| **Fawesome.tv** | https://fawesome.tv — free AVOD; research URL shapes, OG/SSR | **No** | New key + matcher + extractor + tests; probe whether series brand exists in HTML |
| **ITVX** | itv.com / itvx — discussed for later PR; **not** in catalog yet | **No** | New key + website parity + Netflix/Prime-style plugin |
| **4oD / Channel 4** | channel4.com / All4 — user called “Channel 4 OD”; **not** in catalog yet | **No** | Same; watch redirects (channel4.com ↔ all4.com) |
| **Disney+** | disneyplus.com — user-requested; **not** in catalog | **No** | New key + scraper; expect geo/auth walls — document failure modes |
| **Discovery+** | discoveryplus.com — user-requested; **not** in catalog | **No** | Same |
| **Paramount+** | paramountplus.com | **Yes** (`paramountPlus`) | Extractor + `NonPodcastService` + DI + live cases (catalog key exists) |
| **HBO Max** | max.com, hbomax.com | **Yes** (`hboMax`) | Same |
| **Play Suisse** | playsuisse.ch | **Yes** (`playSuisse`) | Same |
| **TVNZ+** | tvnz.co.nz | **Yes** (`tvnzPlus`) | Same |

**Explicitly deferred in the 966 follow-up conversation:** Paramount+, HBO Max, Play Suisse, TVNZ+, Channel 4 OD, ITVX were stopped mid-attempt so 966 could ship with BBC/IA/Vimeo/Netflix/Prime only. Resume that list **plus** fawesome / Disney+ / Discovery+.

**Already done — do not re-implement:** BBC Sounds, BBC iPlayer, Internet Archive, Vimeo, Netflix, Amazon Prime.

**Not this work:** Spotify / Apple / YouTube (API catalogue, not HTML scrapers).

---

## 4. Patterns to copy

### Plugin shape (preferred: Netflix / Amazon Prime)

Per provider library under `Class-Libraries/RedditPodcastPoster.<Provider>/`:

1. **`Matching/*UrlMatcher.cs`** — `IsSubmitUrl(Uri)` (host + path; e.g. Netflix `/title/` or `/watch/`)
2. **`Extractors/*PageMetaDataExtractor.cs`** — HTTP GET + **`OpenGraphPageMetaDataExtractor`** merge; set `Publisher` to platform brand; set `ShowName` only for true series; **null ShowName for films**
3. **`Extensions/ServiceCollectionExtensions.cs`** — `AddHttpClient` + `AddOpenGraphExtractor()` + register `CatalogKeyedNonPodcastServiceAdapter(NonPodcastService.X, ServiceKeys.X, matcher, matcher, extract)`
4. Wire hosts: `Cloud/Api/Ioc.cs`, `Cloud/Indexer/Ioc.cs` (and SubmitUrl console if applicable)

Vimeo is the **oEmbed** variant (`VimeoMetaDataExtractor` → author as publisher). BBC/IA are older path-specific extractors — prefer Netflix/Prime for new SVOD/AVOD sites.

### Shared plumbing

| Path | Role |
|------|------|
| `Models/Podcasts/ServiceCatalog.cs` + `ServiceKeys.cs` | Hosts, compact URLs, ordered catalog |
| `Models/Podcasts/NonPodcastService.cs` | Enum — add new value per extractor service |
| `PodcastServices.Abstractions/.../INonPodcastServiceAdapter*.cs` | Plugin contract |
| `PodcastServices.Abstractions/.../CatalogKeyedNonPodcastServiceAdapter.cs` | Default adapter |
| `PodcastServices/.../NonPodcastServiceAdapterResolver.cs` | Resolve by URL |
| `UrlSubmission/Services/UrlMembershipLookup.cs` | Lookup → `TryExtractShowName` |
| `UrlSubmission/Services/NonPodcastShowNameResolver.cs` | Series vs publisher |
| `UrlSubmission/Services/PodcastNameAttachLookup.cs` | POST name attach |
| `Cloud/Api/Services/SubmitUrl/SubmitUrlService.cs` | 409 on multi-name |
| `OpenGraph/Extractors/OpenGraphPageMetaDataExtractor.cs` | Shared OG parse |
| Website `cultpodcasts/src/app/service-catalog.ts` | Keep key/host/icon parity when adding keys |

### Tests (must follow unit-tests.mdc)

- Plugin unit tests: `RedditPodcastPoster.<Provider>.Tests/BusinessRules/*`
- Resolver / membership: `UrlSubmission.Tests/BusinessRules/UrlSubmission/*`
- Live: extend `StreamingScraperCanonicalCases` + browse pages; keep CI skip env
- Support fakes: `NonPodcastSubmitAdapterResolverSupport`, `LiveStreamingScraperAdapterResolverSupport`
- Guardrails: `pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged`
- Full contract: `.cursor/rules/unit-tests.mdc` (+ enforcement rule)

### Submit-url bigger picture (do not confuse)

    Lookup (URL membership):
      known unique | unknown + optional podcastName | ambiguous 200 + podcastIds

    POST name-only:
      FindByName → 1 attach | >1 → 409 | 0 create with PodcastName

    UI (Curator): extracted podcastName on streaming → POST { url, podcastName }
      (see submit-url-flows.md)

---

## 5. Acceptance criteria

For each new provider you claim done:

1. **Matcher** accepts real watch/title URLs; rejects unrelated hosts/paths.
2. **Extractor** returns title (+ description/image/duration/release when available), correct `Publisher`, and **`ShowName` only when it is a series brand**.
3. **Lookup** returns `kind: streaming` and `podcastName` consistent with `NonPodcastShowNameResolver` — **never** the platform name as series.
4. **Films / one-offs** → `podcastName` / `ShowName` **null** (canonical case with `ExpectedPodcastName: null`).
5. **DI** registered on Api (+ Indexer) so submit/lookup use the adapter.
6. **Catalog + website** keys/hosts/icons aligned if the key is new.
7. **Unit tests** green under unit-tests.mdc; **live Theories** added and skipped in CI via `SKIP_LIVE_STREAMING_SCRAPER_TESTS=1`.
8. Prefer browsing one public homepage/section for harvest if SSR exposes submit links (Prime/Sounds pattern); skip harvest if marketing pages hide deep links (Netflix pattern).

---

## 6. Out of scope / freeze

| Freeze | Rule |
|--------|------|
| **No merge** | Never `gh pr merge` / merge to main unless user asks |
| **No Api/website wrangler deploy** | No `wrangler deploy` / `npm run deploy` for Worker or Pages |
| **No Cosmos episode writes** | No production episode patch/upsert without explicit `--apply` / user approval (episode-guest-handles guardrail) |
| **Catalogue content types epic** | **Planning only** — separate TvShow / Movie / **NewsOrganisation** (British spelling) / NewsReport containers. Do **not** mix container migration or classifier ADR-0004 into the scraper PR unless the user asks |
| **ADR-0004** | Outline only — scrapers still create Podcast + Episode today |
| **Secrets** | If any new secret appears, document names in PR `## Config / secrets` for preview **and** production |
| **This task** | Research + implement scrapers; open PRs when asked — do not commit unless asked |

---

## 7. Suggested first steps

1. Re-read PR 966 summary + `NonPodcastShowNameResolver` + one full plugin (`RedditPodcastPoster.Netflix` or `.AmazonPrime`) end-to-end.
2. Spike **HTML/OG** for **ITVX** and **Channel 4 / All4** (UK priority from prior chat), then **fawesome.tv**, then Disney+/Discovery+ — note geo/login walls and whether series brand is scrapeable.
3. Prefer finishing **catalog-already-present** extractors (Paramount+, HBO Max, Play Suisse, TVNZ+) in parallel only if OG is easy; otherwise prioritize user-named UK + fawesome.
4. For each chosen provider: add `ServiceKeys` / `ServiceCatalog` / `NonPodcastService` → library → DI → canonical cases → run unit tests; run live suite locally with skip env unset.
5. If new catalog keys: mirror `website/.../service-catalog.ts` in the same change set or a sibling website PR; bump website `package.json` if that PR ships client code.
6. Ask the user before production Functions deploy (`deploy-api.ps1`) or any Cosmos write.

**Workspace roots:**  
`C:\Users\jonbr\source\repos\cultpodcasts\RedditPodcastPoster` (primary) · sibling `Api` · sibling `website`
