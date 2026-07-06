# Step 7 checklist — Refactor phases B–F

**Purpose:** Working tracker for implementation phases after the test gate (Steps 1–6).  
**Plan:** [README.md](./README.md) (§3.6 Implementation phases, §6 Step 7, §8 PR checklist, §10 SOLID).

## Rules (every phase)

| Rule | Detail |
|------|--------|
| **One phase per PR** | Do not combine B–F in a single PR |
| **No business-rule assertion changes** | Changing a `DisplayName` Then-clause or assert requires explicit sign-off |
| **Tests are the spec** | Business-rule tests define correct behavior; characterize quirks, do not “fix” during refactor |
| **No accidental behavior change** | Suspected bugs stay as current-behavior rules (§4.7); fixes are separate PRs |

---

## Progress summary

| Phase | Status | Risk | PR |
|-------|--------|------|----|
| **A** — Domain types + applier/merger/matcher (internal) | 🟢 In production / Done | Medium | [#871](https://github.com/cultpodcasts/RedditPodcastPoster/pull/871) |
| **B** — UrlSubmission through applier | 🟢 In production / Done | Medium | [#872](https://github.com/cultpodcasts/RedditPodcastPoster/pull/872) |
| **C** — Platform adapters at boundaries | 🟢 In production (soak) | Medium–High | [#873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873) |
| **D** — Collapse finders into single matcher | 🟢 In production / Done | Medium–High | [#874](https://github.com/cultpodcasts/RedditPodcastPoster/pull/874) |
| **E** — Shared enricher template | 🟡 Indexer deployed (soak) | Medium | [#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875) |
| **F** — Cleanup | ⬜ Not started (blocked on Phase E soak + merge) | Low–Medium | _PR link_ |

---

## Phase 0 / Phase A status

Steps 1–6 are complete. **Phase A** is in production (merged via [PR #871](https://github.com/cultpodcasts/RedditPodcastPoster/pull/871)). **Phase B** is in production (merged via [PR #872](https://github.com/cultpodcasts/RedditPodcastPoster/pull/872)). **Phase C** in production (soak, review pending — [PR #873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873)). **Phase D** in production (merged via [PR #874](https://github.com/cultpodcasts/RedditPodcastPoster/pull/874)). **Phase E** deployed to **Indexer** for soak ([#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875) open — merge after soak review). **Phase F** not started.

| Step / phase | Outcome |
|--------------|---------|
| Steps 1–6 | Test infrastructure, domain types, Layer 1–3 business rules, adapter rules, coverage baseline + CI gate |
| **Phase A** | Domain services in `RedditPodcastPoster.Episodes`; `EpisodeMerger` / `EpisodeMatcher` wired to `EpisodePlatformMatcher` / `EpisodePlatformMerger` |
| Adapters | Wired at provider boundaries (Phase C); resolved-item adapters in UrlSubmission (Phase B) |
| Applier | Used inside Episodes (via merger path) and UrlSubmission `EpisodeEnricher` (Phase B) |

### Phase A checklist

- [x] Domain types + applier / merger / matcher implemented (internal)
- [x] `EpisodeMerger` / `EpisodeMatcher` wired to `EpisodePlatformMatcher` / `EpisodePlatformMerger`
- [x] Steps 1–6 test gate green; coverage baseline + CI gate
- [x] Released via [PR #871](https://github.com/cultpodcasts/RedditPodcastPoster/pull/871)
- [x] Deployed to **Indexer** for soak (2026-07-03 ~20:02 UTC — `indexer-deployment/released-package.zip`)
- [x] Soak review (2026-07-04) — no red flags in production telemetry; safe to merge PR #871

### Risk to production (Phase A — live / soak context)

- **Risk level:** Medium (residual; soak did not surface issues)
- **Blast radius:** Indexer host — match/merge via `EpisodeMerger` → domain matcher/merger/applier
- **What changes live:** Indexing episode match and merge (identity, title/duration, release strategies, fill-missing platform fields on merge)
- **What does not change:** Adapters not live at provider/resolver boundaries; UrlSubmission `EpisodeEnricher` still mutates flat `Episode` directly
- **Residual risks:**
  - Subtle merge drift vs pre-refactor `EpisodeMerger` on edge cases not covered by rules
  - YouTube URL-only backfill paths may diverge if not fully characterized
  - `YouTubePublishDelayMatchStrategy` at 0% coverage — delay-window match behavior under-tested
- **Soak / deploy scope:** Indexer only (deployed 2026-07-03; soak passed 2026-07-04)
- **Soak evidence (App Insights `ai-infra` + metrics `indexer-infra`):**
  - Window: deploy `2026-07-03T20:02:19Z` through review `~2026-07-04T15:00Z` (~19h)
  - Executions steady (~186 / 6h pre- and post-deploy; no drop)
  - Zero `exceptions` for `indexer-infra`; zero traces with `Failed to ingest` (ambiguous multi-match `LogError`)
  - Zero severity≥3 traces; no DI / `NullReference` / `ArgumentException` messages in episode-domain paths
  - Visible warnings were Bluesky posting only (unrelated to match/merge)
  - Caveat: `RedditPodcastPoster` log level is Warning + 25% OTel sampling — successful merge `LogInformation` not visible; red-flag path is `LogError` (`Failed to ingest`) which would still export
- **Rollback notes:** Revert Phase A wiring (`EpisodeMerger` / `EpisodeMatcher` → domain services); no UrlSubmission rollback needed

---

## Phase B — UrlSubmission through applier

**Goal:** `EpisodeEnricher` applies platform fields via `EpisodePlatformApplier` (and resolved-item adapters) instead of ad-hoc mutation of flat `Episode` properties.

**Scope / areas:**

- `RedditPodcastPoster.UrlSubmission/EpisodeEnricher.cs` (and DI registration)
- `Resolved*ItemAdapter` usage for `CategorisedItem` → `EpisodeCandidate` / `EpisodePlatformPatch`
- UrlSubmission business rules: `UrlSubmission.Tests/BusinessRules/UrlSubmission/`

**Preconditions:**

- [x] Phase A done (`EpisodePlatformApplier` exists and is covered by domain rules)
- [x] `ResolvedSpotifyItemAdapter`, `ResolvedAppleItemAdapter`, `ResolvedYouTubeItemAdapter` exist with Layer 1 rules
- [x] UrlSubmission enrichment/persistence rules green (§5.6)

### Checklist

- [x] Inject `IEpisodePlatformApplier` (and adapters as needed) into `EpisodeEnricher`
- [x] Map each `Resolved*Item` on `CategorisedItem` through the corresponding adapter → candidate/patch
- [x] Apply missing platform links (ID, URL, image) via applier — no direct `matchingEpisode.SpotifyId = …` style writes for platform fields
- [x] Preserve podcast-level enrichment (show IDs, etc.) and non-platform episode fields (description helper, BBC/IA if still special-cased) without regressing rules
- [x] Update UrlSubmission test construction to supply applier (real implementation, not a mock that hides behavior)
- [x] Confirm no new processing logic outside adapters / applier for platform field writes
- [x] **DI:** register episodes domain at composition root, not inside `AddUrlSubmission()`

### DI registration (Phase B)

| Extension | Registers |
|-----------|-----------|
| `AddEpisodesDomain()` | `IEpisodePlatformApplier`, `IEpisodePlatformMerger`, `IEpisodePlatformMatcher`, match strategies, merge policies |
| `AddRepositories()` | Cosmos repositories and legacy `EpisodeMatcher` / `EpisodeMerger` only — **does not** call `AddEpisodesDomain()` |
| `AddUrlSubmission()` | UrlSubmission services only (including `IEpisodeEnricher`); **does not** register episodes domain |

**Hosts that call both** `AddEpisodesDomain()` and `AddRepositories()` explicitly:

| Host | Composition root | Why |
|------|------------------|-----|
| Api | `Cloud/Api/Ioc.cs` | `AddUrlSubmission`, `AddPodcastServices`, `AddIndexer` → applier + merger |
| Indexer (cloud) | `Cloud/Indexer/Ioc.cs` | `AddPodcastServices` → `PodcastUpdater` / `IEpisodeMerger` |
| Index CLI | `Console-Apps/Index/Program.cs` | `AddIndexer` + `AddPodcastServices` |
| SubmitUrl CLI | `Console-Apps/SubmitUrl/Program.cs` | `AddUrlSubmission` + `AddPodcastServices` |
| Enrich existing episodes CLI | `Console-Apps/EnrichExistingEpisodesFromPodcastServices/Program.cs` | `AddUrlSubmission` → applier |
| Wikipedia episode enricher CLI | `Console-Apps/WikipediaEpisodeEnricher/Program.cs` | `AddUrlSubmission` + `AddPodcastServices` |
| Poster, AddAudioPodcast, EnrichPodcastWithImages, WebsubStatus CLIs | respective `Program.cs` | `AddPodcastServices` → merger |
| EnrichYouTubeOnlyPodcasts, FixDatesFromApple, TextClassifierTraining, AddYouTubeChannelAsPodcast, ThrowawayConsole CLIs | respective `Program.cs` | platform episode providers → catalogue adapters (Phase C) |

**Repos-only hosts** (no `AddEpisodesDomain()` — do not resolve matcher/merger/applier):

| Host | Composition root |
|------|------------------|
| Discovery (cloud) | `Cloud/Discovery/Ioc.cs` |
| Discover CLI | `Console-Apps/Discover/Program.cs` |
| Other Cosmos maintenance/backfill CLIs | e.g. `CosmosDbUploader`, `SeedKnownTerms`, `FindDuplicateEpisodes`, … |

**Rationale:** keep feature extensions (`AddUrlSubmission`, future enrich templates) focused on their pipeline; domain services stay explicit at the host composition root so callers choose matcher/merger/applier registration independently of persistence.

### Exit criteria

- [x] All UrlSubmission business-rule tests pass **without assertion changes**
- [x] Full Step 7 test set green (Episodes, PodcastServices, UrlSubmission, Persistence)
- [x] `./scripts/coverage-gate.ps1` passes (no regression below baseline)
- [x] PR opened for Phase B only
- [x] Deployed for overnight soak (2026-07-04)
- [x] Soak review (2026-07-05) — no red flags in production telemetry; safe to merge PR #872

### Phase B deploy / soak status

- [x] Branch deployed from [PR #872](https://github.com/cultpodcasts/RedditPodcastPoster/pull/872) (2026-07-04 overnight soak)
- [x] **Api** — UrlSubmission Phase B (`EpisodeEnricher` → applier + resolved-item adapters)
- [x] **Indexer** — explicit `AddEpisodesDomain()` + Phase A merge path
- [x] **Discovery** — repos-only (no `AddEpisodesDomain()`)
- [x] **Publishing console apps** — Poster etc. with explicit `AddEpisodesDomain()`
- [x] Soak review (2026-07-05) — no red flags; safe to merge PR #872

### Risk to production (Phase B — live / soak context)

- **Risk level:** Medium
- **Blast radius:** UrlSubmission path on **Api** (`EpisodeEnricher` → `EpisodePlatformApplier` + resolved-item adapters); **Indexer** and publishing console apps carry explicit `AddEpisodesDomain()` registration; **Discovery** repos-only (unchanged domain wiring)
- **What changes live:** How submitted URLs enrich existing episodes (platform ID/URL/image writes go through applier); composition roots updated for explicit domain registration on deployed hosts
- **What does not change:** Indexer match/merge algorithms (Phase A, unchanged by B); catalogue adapters still not wired at provider/resolver boundaries; Discovery does not resolve matcher/merger/applier
- **Residual risks:**
  - Podcast-level or non-platform fields (description, BBC/IA) regress if special-cases are dropped during the move
  - Resolved-item adapter quirks surface only on live submit traffic not covered by rules
- **Soak / deploy scope:** Api, Indexer, Discovery, publishing console apps (deployed 2026-07-04; soak passed 2026-07-05)
- **Soak evidence (App Insights `ai-infra` + Log Analytics `loganalytics-infra`):**
  - Deploy blobs: `indexer-deployment` 2026-07-04T20:37:39Z, `api-deployment` 2026-07-04T20:42:55Z, `discovery-deployment` 2026-07-04T20:44:54Z
  - Window: deploy through review ~2026-07-05T10:30Z (~14h)
  - Zero `AppExceptions` for `api-infra`, `indexer-infra`, `discover-infra`
  - Zero traces with `Failed to ingest`; zero episode-domain / UrlSubmission / DI resolve failures
  - Severity≥3 traces are pre-existing noise only (Twitter credits depleted, YouTube channel-not-found, indexer `No updates` LogError) — not Phase B paths
  - **Api happy path:** 4× `POST api/SubmitUrl` (all HTTP 200, post-deploy); 2× `POST api/DiscoveryCuration` (200); api cold-start loaded 28 functions including `SubmitUrl` (DI OK)
  - **Indexer happy path:** 3× `Hourly` + 2× `HalfHourly` post-deploy, all success; full activity chain (Indexer, Categoriser, Poster, Publisher, Tweet, Bluesky)
  - **Discovery happy path:** 1× `DiscoveryTrigger` → `Discover` post-deploy, success; repos-only wiring unchanged (no domain DI expected)
  - Caveat: `RedditPodcastPoster` log level Warning + 25% OTel sampling — successful applier `LogInformation` not visible; red-flag path is exceptions / `LogError` which would still export
- **Rollback notes:** Revert `EpisodeEnricher` to direct flat-field mutation; revert explicit `AddEpisodesDomain()` at affected composition roots if needed

**PR:** [#872](https://github.com/cultpodcasts/RedditPodcastPoster/pull/872)

### Phase B remaining deferred test gaps (address in Phase F)

| Gap | Location | Address in | Priority | Status |
|-----|----------|------------|----------|--------|
| UrlSubmission release/description parity with indexing applicator | `UrlSubmission/EpisodeEnricher.cs` | Phase F P0 — `UrlSubmissionEnrichmentRules` | P0 | [x] |
| BBC / Internet Archive / non-podcast image paths | `EpisodeEnricher.cs` (~202–248) | Phase F P0 — `UrlSubmissionEnrichmentRules` | P0 | [x] |
| Remaining `EpisodeEnricher` orchestration branches | `EpisodeEnricher.cs` (43% branch floor) | Phase F P1 — extend rules; raise toward 85% aspiration | P1 | Open |
| Legacy `EpisodeReleaseMatchTolerance` (Abstractions) call sites on submit path | UrlSubmission categorisers | Phase F P1 — tolerance parity before removal | P1 | [x] Characterized — removal deferred to Phase F cleanup |

---

## Phase C — Platform adapters at provider/resolver boundaries

**Goal:** Providers and resolvers map foreign API types to `EpisodeCandidate` at the boundary (thin adapters), so downstream indexing code no longer depends on Spotify/Apple/YouTube API types for episode processing.

**Scope / areas:**

- Catalogue adapters: `SpotifyEpisodeAdapter`, `AppleEpisodeAdapter`, `YouTubeEpisodeAdapter`
- `EpisodeProvider` / platform episode providers and resolvers that currently produce or consume `Episode` via `Episode.From*` or raw API types
- Platform projects under `PodcastServices.Spotify`, `PodcastServices.Apple`, `PodcastServices.YouTube`
- Adapter rules: `Episodes.Tests/BusinessRules/Adapters/`

**Preconditions:**

- [x] Phase A done
- [x] Catalogue adapters + Layer 1 rules exist
- [x] Phase B merged (UrlSubmission path already uses adapters/applier pattern)

### Checklist

- [x] Wire Spotify catalogue path: API/`FullEpisode` (or existing input DTO) → `SpotifyEpisodeAdapter` → `EpisodeCandidate` at provider/resolver boundary
- [x] Wire Apple catalogue path: `AppleEpisode` (or input DTO) → `AppleEpisodeAdapter` → `EpisodeCandidate`
- [x] Wire YouTube catalogue path: `SearchResult` / `PlaylistItem` (or input DTOs) → `YouTubeEpisodeAdapter` → `EpisodeCandidate`
- [x] Convert candidates to persisted `Episode` only via applier/merger (or a single documented factory), not scattered `Episode.From*` in orchestration
- [x] Keep platform-specific flags at the boundary (`Spotify` expensive-query, etc.) — do not push into matcher/merger
- [x] Update any provider/resolver unit tests to assert candidate/episode outcomes, not adapter internals alone

### Exit criteria

- [x] Adapter business-rule tests pass **without assertion changes**
- [x] Matching/merging/indexing/UrlSubmission rules still pass **without assertion changes**
- [x] `./scripts/coverage-gate.ps1` passes
- [x] PR opened for Phase C only
- [x] Pre-soak business-rule gap tests added (+15): UrlSubmission persistence (2), `EpisodePlatformApplierRules` (6), provider round-trips (3), Resolved Apple URL-only (2), `YouTubePublishDelayMatchStrategyRules` (3 incl. Spotify negative); Episodes.Tests 91, UrlSubmission.Tests 20
- [x] Deployed for soak (2026-07-05)
- [ ] Soak review pending

**Phase C soak incident (2026-07-05):** `enrichyouTubeOnlyPodcasts` failed at runtime — `Unable to resolve service for type 'IEpisodeCatalogueAdapter<YouTubeCatalogueInput>'` when activating `YouTubeEpisodeProvider`. Root cause: Phase C wired providers to inject catalogue adapters from `AddEpisodesDomain()`, but `EnrichYouTubeOnlyPodcasts` (and other platform-service CLIs) never called it explicitly. Fixed: add `AddEpisodesDomain()` at composition root before `AddYouTubeServices` / `AddAppleServices` / `AddSpotifyServices` on affected console apps (`EnrichYouTubeOnlyPodcasts`, `FixDatesFromApple`, `TextClassifierTraining`, `AddYouTubeChannelAsPodcast`, `ThrowawayConsole`).

### Phase C remaining deferred test gaps (address in Phase F)

| Gap | Location | Address in | Priority | Status |
|-----|----------|------------|----------|--------|
| `PlatformLinkFactory` null-all-inputs path | `Episodes/Adapters/PlatformLinkFactory.cs` | Phase F P2 — one adapter rule | P2 | [x] |
| `ResolvedAppleItemAdapter` URL-only negative branch | `ResolvedAppleItemAdapter.cs` (50% branch) | Phase F P2 — optional rule | P2 | Open |
| Phase C soak review | Deploy hosts | Operational — not a test gap | — | Pending |

### Phase C deploy / soak status

- [x] Branch deployed from [PR #873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873) (2026-07-05)
- [x] **Api** — Phase C soak (shared catalogue adapter wiring at provider/resolver boundaries)
- [x] **Indexer** — catalogue providers → adapters → factory (`SpotifyEpisodeAdapter`, `AppleEpisodeAdapter`, `YouTubeEpisodeAdapter` at provider/resolver boundaries)
- [x] **Discovery** — Phase C soak (deployed with full soak scope)
- [ ] **Publishing console apps** — deploying (Poster etc.)
- [ ] Soak review pending — will revisit after soak

### Risk to production (Phase C — live / soak context)

- **Risk level:** Medium–High
- **Blast radius:** Indexer (all platform providers/resolvers); any host that builds episodes from catalogue API types; Api / Discovery deployed for soak alongside Indexer and publishing console apps
- **What changes live:** Spotify/Apple/YouTube catalogue → `EpisodeCandidate` at the boundary; `Episode.From*` / raw API types leave the indexing processing path
- **What does not change:** Finder scoring logic (still Phase D); enricher mutation patterns (still Phase E); UrlSubmission already on adapters from Phase B
- **Residual risks:**
  - Adapter mapping quirks (IDs, URLs, release shapes) affect every indexed episode for all platforms
  - Scattered `Episode.From*` leftovers produce divergent episode shapes if not fully removed
  - Platform-specific flags (e.g. Spotify expensive-query) misplaced into matcher/merger
- **Soak / deploy scope:** Api, Indexer, Discovery, publishing console apps (deployed 2026-07-05; publishing console apps deploying; soak in progress — review pending, will revisit after soak)
- **Rollback notes:** Revert provider/resolver wiring to pre-adapter `Episode.From*` / API-type path; Phase A/B domain services can remain

**PR:** [#873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873)

---

## Phase D — Collapse finders into single matcher

**Goal:** Single `EpisodePlatformMatcher` owns match logic; platform finders become thin wrappers (or call sites) over the domain matcher + release strategies — delete duplicate finder scoring code.

**Scope / areas:**

- `EpisodePlatformMatcher` + `IReleaseMatchStrategy` implementations
- Spotify: `SearchResultFinder` / `ISearchResultFinder`
- YouTube: `SearchResultFinder`, `PlaylistItemFinder`
- Apple finder/enricher match paths (any parallel title/duration/release logic)
- Existing `EpisodeReleaseMatchTolerance` call sites used only for matching (migrate per §10.9 — copy semantics, do not rewrite)
- Matcher rules: `Episodes.Tests/BusinessRules/Matching/`

**Preconditions:**

- [x] Phase C merged (candidates available at boundaries)
- [x] Matcher rule catalog (§5.1) green against `EpisodePlatformMatcher`

### Checklist

- [x] Inventory all finder/match entry points that duplicate identity, title/duration, or release tolerance logic
- [x] Route Spotify finder matching through `IEpisodePlatformMatcher` (candidates from adapters)
- [x] Route YouTube search/playlist finder matching through `IEpisodePlatformMatcher`
- [x] Route Apple match paths through `IEpisodePlatformMatcher`
- [x] Move remaining `EpisodeReleaseMatchTolerance` **match-time** methods into `IReleaseMatchStrategy` classes without semantic changes (§10.9) — Apple enricher reducer now uses `CatalogueReleaseMatches`
- [x] Leave finders as thin wrappers (resolve candidates + call matcher) or delete if fully superseded
- [x] Document strategy registration order if DI order changes (first applicable wins) — unchanged: Exact → SpotifyCatalogue → YouTubePublishDelay

### Exit criteria

- [x] Matcher business-rule tests pass **without assertion changes**
- [x] Duplicate finder scoring/tolerance code removed (no parallel implementations left)
- [x] Orchestration/indexing rules still pass **without assertion changes**
- [x] `./scripts/coverage-gate.ps1` passes (baselines updated in `coverage-baseline.json`)
- [x] PR opened for Phase D only ([#874](https://github.com/cultpodcasts/RedditPodcastPoster/pull/874))

### Deferred test gaps (documented — not blocking Phase D merge)

Pre-soak characterization added in PR #874 follow-up (P0–P3):

| Gap | Location | Status |
|-----|----------|--------|
| Exact-title bypass of release tolerance | `EpisodePlatformMatcher.MatchesByTitleHeuristics` | Characterized — `CatalogueMatchingRules.exact_title_match_accepts_despite_mismatched_release_and_duration` |
| Cross-platform `IsCatalogueMatch` (negative-delay aligned Spotify; positive-delay misaligned YouTube) | `EpisodePlatformMatcher.IsCatalogueMatch` | Covered — `is_catalogue_match_accepts_negative_delay_aligned_spotify_catalogue`, `is_catalogue_match_rejects_positive_delay_misaligned_youtube_catalogue` |
| Spotify enricher `CatalogueReleaseMatches` reducer | `SpotifyEpisodeEnricher` | Covered — `SpotifyEpisodeEnricherCatalogueReleaseReducerRules.enrich_filters_candidates_via_catalogue_release_reducer` |
| Stored YouTube + incoming Spotify (strategy lines 28–37) | `SpotifyCatalogueReleaseMatchStrategy` | Covered — `YouTubePublishDelayMatchStrategyRules.negative_delay_youtube_stored_spotify_incoming_aligned` |

### Phase D remaining deferred test gaps (address in Phase F)

| Gap | Location | Address in | Priority | Status |
|-----|----------|------------|----------|--------|
| Exact-title bypass extended matrix | `EpisodePlatformMatcher.MatchesByTitleHeuristics` | Phase F P2 — `CatalogueMatchingRules` | P2 | [x] |
| `SpotifyCatalogueReleaseMatchStrategy` direct rules | Strategy file (50% branch) | Phase F P1 — new rule file | P1 | [x] |
| `ExactReleaseMatchStrategy` direct rules | Strategy file (75% branch) | Phase F P1 — new rule file | P1 | [x] |
| `YouTubePublishDelayMatchStrategy` lines 30–36 | Unreachable when incoming is YouTube | Phase F P3 — document-only | P3 | [x] |
| `PlaylistItemFinder` wrapper + local fuzzy paths | `PlaylistItemFinder.cs` | Phase F P0 — `PlaylistItemFinderCatalogueWrapperRules` (16 rules) | P0 | [x] |
| YouTube `SearchResultFinder` wrapper rules | `SearchResultFinder.cs` | Phase F P3 — mirror Spotify wrapper pattern if finder retired | P3 | Open |
| `EpisodePlatformMatcher` branch aspiration (70% → 90%) | `EpisodePlatformMatcher.cs` | Phase F P3 — incremental catalogue rules | P3 | Open |

### Phase D test additions (PR #874)

- `CatalogueMatchingRules` — 12 rules (catalogue lookup, release filter, `IsCatalogueMatch`)
- `EpisodeMappingExtensionsRules` — 7 rules (stored → candidate/patch mapping)
- `SearchResultFinderCatalogueWrapperRules` — 3 rules (Spotify thin wrapper delegation)
- Episodes.Tests: 110 total; coverage gate episodes-domain **74.9% branch / 92.1% line**

### Phase D pre-soak test additions (PR #874 follow-up)

| Priority | Rule / test | Regression vector |
|----------|-------------|-------------------|
| P0 | `CatalogueMatchingRules.exact_title_match_accepts_despite_mismatched_release_and_duration` | Exact-title bypass before release tolerance |
| P1 | `is_catalogue_match_accepts_negative_delay_aligned_spotify_catalogue`, `is_catalogue_match_rejects_positive_delay_misaligned_youtube_catalogue` | Cross-platform `IsCatalogueMatch` |
| P2 | `SpotifyEpisodeEnricherCatalogueReleaseReducerRules.enrich_filters_candidates_via_catalogue_release_reducer` | Spotify enricher `CatalogueReleaseMatches` reducer |
| P3 | `YouTubePublishDelayMatchStrategyRules.negative_delay_youtube_stored_spotify_incoming_aligned` | Stored YouTube + incoming Spotify (`SpotifyCatalogueReleaseMatchStrategy` lines 28–37) |

### Phase D regression-hardening (follow-up on #874 — uncommitted)

Additional rules pinning high-risk Phase D collapse vectors:

| Rule file | New rules | Regression vector |
|-----------|-----------|-------------------|
| `CatalogueMatchingRules` | +8 | Cross-platform `IsCatalogueMatch` (negative-delay aligned Spotify; positive-delay misaligned YouTube when titles differ); 12h release-only YouTube-discovered fallback; same-length fuzzy disambiguation; zero-length missed-match guard; HTML entity decode; date-lookup reducer |
| `YouTubePublishDelayMatchStrategyRules` | +1 | Stored Spotify + incoming YouTube aligned under positive delay (lines 23–27) |
| `AppleEpisodeResolverCatalogueWrapperRules` | +1 | Apple thin-wrapper delegates YouTube-discovered unique-duration to domain matcher |

- Episodes.Tests: **160** total; coverage gate episodes-domain **81.9% branch / 94.6% line**
- `coverage-baseline.json` per-file floors corrected to measured Release values (prior file baselines for `EpisodePlatformMatcher.cs`, `YouTubePublishDelayMatchStrategy`, `EpisodeMappingExtensions` were aspirational)

### Risk to production

- **Risk level:** Medium–High
- **Blast radius:** Indexer — all platform finders (Spotify search, YouTube search/playlist, Apple match paths); any enrich path that uses finders to attach platform links
- **What changes live:** Match decisions (identity, title/duration, release tolerance) unify on `EpisodePlatformMatcher` + strategies; duplicate finder scoring deleted
- **What does not change:** How fields are written once a match is found (applier/enricher template still Phase E); UrlSubmission match-to-existing if it does not use these finders
- **Residual risks:**
  - Cross-platform match rate drift (false merges or missed links) if a finder quirk was not characterized
  - Strategy DI order changes alter “first applicable wins” behavior
  - `EpisodeReleaseMatchTolerance` migration copies wrong overload or leaves a parallel call site
- **Recommended soak / deploy scope:** Indexer only; compare match/merge outcomes vs pre-deploy baseline if available
- **Rollback notes:** Restore platform finders’ pre-collapse scoring; keep domain matcher for EpisodeMerger path if still correct

**PR:** [#874](https://github.com/cultpodcasts/RedditPodcastPoster/pull/874)

---

## Phase E — Shared enricher template

**Goal:** Shared enrich flow for indexing; platform enrichers supply adapter + resolver and return `EpisodePlatformPatch` — applier writes flat fields once. Orchestration stays thin (`PodcastServicesEpisodeEnricher` / template coordinates only).

**Scope / areas:**

- `PodcastServicesEpisodeEnricher` and platform enrichers (`ISpotifyEpisodeEnricher`, `IAppleEpisodeEnricher`, `IYouTubeEpisodeEnricher`)
- Shared enrich template / `IPlatformEpisodeEnricher` pattern (§10.1, §10.4)
- Optional `IEnrichmentSideEffect` for Spotify expensive-query (not bundled into applier)
- `SkipEnrichingFromYouTube` and delayed-publishing second pass remain orchestration concerns
- Enrichment rules: `PodcastServices.Tests/BusinessRules/Enrichment/`

**Preconditions:**

- [x] Phase D merged (matching is domain-owned)
- [x] Indexing enrichment rules (§5.4) green

### Checklist

- [x] Introduce `IPlatformEnrichmentApplicator` — resolve candidate → `EpisodePlatformPatch` → `IEpisodePlatformApplier` (+ merge policies for release backfill)
- [x] Refactor Spotify enricher to apply patch via applicator (catalogue adapter + `SpotifyExpensiveQuerySideEffect`)
- [x] Refactor Apple enricher to apply patch via applicator
- [x] Refactor YouTube enricher — platform links/release via applicator; description/image via applier fill-missing
- [x] Extract shared enrich template base (`PlatformEpisodeEnricherTemplate` — delayed-publishing bypass + `ApplyResolvedCandidate`)
- [x] Keep delayed-publishing second pass and batch exclusion in orchestrator (not in platform enrichers)
- [x] Extract Spotify expensive-query persistence to `ISpotifyEnrichmentSideEffect`
- [x] Update `PodcastServicesEpisodeEnricherTestSupport` — patch-applying mock factories per platform

### Phase E test additions

| Rule file | Tests | Business rule |
|-----------|-------|---------------|
| `PlatformEnrichmentApplicatorRules` | 13 | Fill-missing links (Spotify/Apple/YouTube); empty/truncated description; supplemental YouTube image; Apple time backfill; YouTube authority preserve; no-identity backfill guard |
| `PlatformEpisodeEnricherTemplateRules` | 2 | Delayed-publishing bypass; ApplyResolvedCandidate → context + episode |
| `PlatformEnrichmentResultExtensionsRules` | 3 | ApplyTo URL flags per service; release propagation; None unchanged |
| `SpotifyExpensiveQuerySideEffectRules` | 2 | Expensive-query flag set / not set |
| `YouTubeEpisodeEnricherCatalogueRules` | 5 | ID/URL backfill; video details; regex sanitization; release backfill |
| `IndexingEnrichmentRules` | +2 | Patch-applying mock; multi-platform Spotify→Apple order |
| `PodcastServicesEpisodeEnricherTestSupport` | 3 helpers | Mocks apply real adapter + applicator patches |
| Existing enricher/orchestration rules | unchanged assertions | §5.4 indexing enrichment still green |

### Phase E deferred test gaps — implementation plan (P0–P3)

Pre-soak characterization for Phase E (#875) gap analysis:

| Priority | Area | Test file | Rules | Status |
|----------|------|-----------|-------|--------|
| P0 | `PlatformEpisodeEnricherTemplate.IsBypassedByDelayedYouTubePublishing` | `PodcastServices.Tests/BusinessRules/Enrichment/PlatformEpisodeEnricherTemplateRules.cs` | Bypass when inside delayed-publishing window | [x] |
| P0 | `PlatformEpisodeEnricherTemplate.ApplyResolvedCandidate` | `PodcastServices.Tests/BusinessRules/Enrichment/PlatformEpisodeEnricherTemplateRules.cs` | Updates `EnrichmentContext` (Spotify URL flag + episode state) | [x] |
| P0 | `SpotifyExpensiveQuerySideEffect` | `PodcastServices.Spotify.Tests/Enrichers/SpotifyExpensiveQuerySideEffectRules.cs` | Sets `SpotifyEpisodesQueryIsExpensive` when expensive; not set when false | [x] |
| P1 | `PlatformEnrichmentResultExtensions.ApplyTo` | `PodcastServices.Tests/BusinessRules/Enrichment/PlatformEnrichmentResultExtensionsRules.cs` | Spotify/Apple/YouTube URL flags; `ReleaseUpdated` → context release | [x] |
| P1 | Applicator truncated description | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | Extends description ending in `...` via `Apply` / `ApplyDescription` | [x] |
| P1 | Applicator null `SourceLink` | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | `Apply` with null link → `None` | [x] |
| P1 | Applicator Apple fill-missing | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | Full `Apply` fills missing Apple link | [x] |
| P1 | Applicator YouTube fill-missing | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | Full `Apply` fills missing YouTube link | [x] |
| P1 | Applicator supplemental link no overwrite | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | `ApplySupplementalLink` skips when image exists | [x] |
| P1 | YouTube enricher catalogue paths | `PodcastServices.YouTube.Tests/Enrichment/YouTubeEpisodeEnricherCatalogueRules.cs` | ID-only URL backfill; URL-only ID backfill; video details; regex sanitization; release backfill | [x] |
| P2 | Patch-applying mock orchestration | `PodcastServices.Tests/BusinessRules/Enrichment/IndexingEnrichmentRules.cs` | `CreateSpotifyEnricherMockApplyingPatch` updates episode state via real applicator | [x] |
| P2 | Multi-platform enrich order | `PodcastServices.Tests/BusinessRules/Enrichment/IndexingEnrichmentRules.cs` | Spotify then Apple on same episode; no cross-overwrite | [x] |
| P3 | `coverage-baseline.json` Phase E floors | `plans/episode-domain-refactor/coverage-baseline.json` | `platform-enrichment` group + 7 per-file floors (template, ApplyTo, side effect, enrichers, PlaylistItemFinder); gate includes Spotify/YouTube/Apple.Tests | [x] |
| P3 | `AppleTimeBackfillMergePolicy` branch | `Episodes.Tests/BusinessRules/Applying/PlatformEnrichmentApplicatorRules.cs` | No release backfill when incoming candidate has no Apple identity | [x] |
| P3 | Phase D deferred cross-phase items | (this checklist) | Document still-relevant Phase D gaps — do not re-implement unless trivial | [x] |

#### Phase E remaining deferred (address in Phase F)

| Gap | Location | Address in | Priority | Status |
|-----|----------|------------|----------|--------|
| Spotify enricher full catalogue E2E | `SpotifyEpisodeEnricher.cs` | Phase F P1 | P1 | [x] (#875) |
| Apple enricher E2E + test convention debt | `AppleEpisodeEnricher.cs`, `AppleEpisodeEnricherTests.cs` | Phase F P1 / P2 | P1 | [x] (#875) |
| Apple/YouTube patch-applying orchestration mocks | `IndexingEnrichmentRules.cs` | Phase F P1 | P1 | [x] (#875) |
| Template bypass/apply per Apple/YouTube enricher (optional) | Platform enrichers inherit template | Phase F P3 optional | P3 | Open — optional; template base covered |
| `PlaylistItemFinder` local fuzzy/duration paths below aspiration | `PlaylistItemFinder.cs` (44% branch) | Phase F P3 — more Theory rows or retire wrapper | P3 | Open |

#### Phase D deferred cross-phase items (still relevant after Phase E)

These Phase D gaps remain open for later phases; Phase E tests do not re-implement them unless trivial:

| Gap | Location | Address in | Status |
|-----|----------|------------|--------|
| Exact-title bypass edge cases beyond current matrix | `EpisodePlatformMatcher.MatchesByTitleHeuristics` | Phase F P2 — `CatalogueMatchingRules` extension | [x] |
| `YouTubePublishDelayMatchStrategy` lines 30–36 unreachable when incoming is YouTube | Strategy registration order | Phase F P3 — document-only unless order changes | [x] |
| Release strategy direct characterization | `SpotifyCatalogueReleaseMatchStrategy`, `ExactReleaseMatchStrategy` | Phase F P1 — new rule files | [x] |
| `EpisodePlatformMatcher` branch below 90% aspiration | `EpisodePlatformMatcher.cs` (70% branch) | Phase F P3 — incremental catalogue rules | Open |
| Orchestration branch gaps | `EpisodeEnricher`, `PodcastUpdater` | Phase B/F — see master index | Partial — `PodcastUpdater` [x] (#875); `EpisodeEnricher` branches Open |
| `ResolvedAppleItemAdapter` branch 50% | Adapters | Phase F P2 — optional negative branch | Open |

### Exit criteria

- [x] Enrichment business-rule tests pass **without assertion changes**
- [x] Indexing orchestration/persistence rules pass **without assertion changes**
- [x] No enricher path mutates platform fields without going through applier
- [x] `./scripts/coverage-gate.ps1` passes
- [x] PR opened for Phase E only ([#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875))
- [x] Deployed to **Indexer** for soak (2026-07-06 — branch `feature/episode-domain-phase-e-shared-enricher-template` / `indexer-deployment`)
- [ ] Soak review pending — compare enrichment outcomes vs pre-deploy; watch platform link backfill, expensive-query flags, delayed-publishing second pass

### Phase E deploy / soak status

- [x] Branch deployed from [PR #875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875) (2026-07-06)
- [x] **Indexer only** — `PodcastServicesEpisodeEnricher` + shared enrich template + platform enrichers (Spotify, Apple, YouTube)
- [ ] **Api** — not deployed (UrlSubmission unchanged; Phase B path)
- [ ] **Discovery** — not deployed (out of scope)
- [ ] Soak review pending — will revisit after soak window

**Soak watch list (Indexer):**

- Platform link backfill (ID / URL / image) via `PlatformEnrichmentApplicator`
- Spotify `SpotifyEpisodesQueryIsExpensive` side-effect persistence
- Delayed-publishing second pass and `SkipEnrichingFromYouTube` orchestration (unchanged in orchestrator)
- Multi-platform enrich order (Spotify → Apple → YouTube on same episode)
- Enrichment exceptions / `Failed to ingest` (same red-flag path as prior soaks)

### Risk to production (Phase E — live / soak context)

- **Risk level:** Medium
- **Blast radius:** Indexer — `PodcastServicesEpisodeEnricher` and platform enrichers (Spotify, Apple, YouTube)
- **What changes live:** Shared enrich template (resolve → adapt → patch → applier); platform enrichers no longer mutate flat `Episode` platform fields in place
- **What does not change:** Match logic (Phase D); UrlSubmission enrich path (Phase B); discovery enrichers (out of scope)
- **Residual risks:**
  - `SkipEnrichingFromYouTube` or delayed-publishing second pass moved into enrichers by mistake
  - Spotify expensive-query side-effect lost or double-applied
  - Patch apply order differs from prior in-place mutation for multi-platform enrich
- **Soak / deploy scope:** Indexer only (deployed 2026-07-06; soak in progress — review pending)
- **Rollback notes:** Restore platform enrichers’ direct mutation; shared template can be removed without touching matcher/merger

**PR:** [#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875)

---

## Phase F — Cleanup

**Goal:** Unify remaining mappers, rename homonyms, retire transitional wrappers. No behavior change.

**Scope / areas:**

- Any remaining `Resolved*Item` / catalogue mappers that bypass adapters
- Homonymous types/names (e.g. parallel `SearchResultFinder`s, legacy `EpisodeMatcher` surface if fully superseded)
- Thin wrappers left from Phases D–E that no longer add value
- Dead `EpisodeReleaseMatchTolerance` static surface once all call sites migrated
- Naming/DI cleanup in `AddEpisodesDomain` / composition roots

**Preconditions:**

- [ ] Phases B–E merged (Phase E: Indexer soak in progress — [#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875) merge after soak review)
- [ ] No production call sites depend on retired wrappers
- [x] **P0 test backlog complete** (see below — do not start Phase F cleanup until Phase E soak passes and #875 merges)

### Phase F pre-requisites — final test gap audit (2026-07-06)

Final audit before Phase F. **§5 catalog (57 rules) is complete.** Phase E P0–P3 hardening is done. Residual risk is orchestration parity and wrapper characterization, not domain merge/match core.

**Decision:** Do not start Phase F wrapper retirement until Phase E soak review passes and [#875](https://github.com/cultpodcasts/RedditPodcastPoster/pull/875) merges. Pre-soak test backlog (P0–P3) is complete in #875; remaining work is cleanup code + coverage aspiration (see deferred actions register below).

#### Adequately covered (no action)

- §5.1–§5.6 catalog — all rules have `[Fact]`/`[Theory]` tests
- Domain merge/match/applier core; platform catalogue adapters (98–100% line)
- Phase D collapse (`CatalogueMatchingRules`, Spotify/Apple wrapper rules, fuzzy title matrix)
- Phase E template/applicator/side-effect/YouTube enricher/orchestration hardening

#### Test backlog (ordered — implement before Phase F)

| Priority | Item | Test target | Effort | Done |
|----------|------|-------------|--------|------|
| **P0** | UrlSubmission enrich parity | Extend `UrlSubmissionEnrichmentRules` — Apple/YouTube release backfill + truncated description (parity with `PlatformEnrichmentApplicator`) | M | [x] |
| **P0** | `PlaylistItemFinder` characterization | New wrapper rules (mirror `SearchResultFinderCatalogueWrapperRules`) — publish-delay catalogue match, exact-title → `IsCatalogueMatch` | L | [x] |
| **P0** | UrlSubmission BBC / IA / non-podcast image | Extend `UrlSubmissionEnrichmentRules` — special-cased paths in `EpisodeEnricher` (lines ~202–248); README §4.7 bug risk | S | [x] |
| **P1** | Tolerance parity / migration | `EpisodeReleaseTolerance` matrix + legacy `EpisodeReleaseMatchTolerance` call-site characterization before type removal | M | [x] |
| **P1** | Release strategy direct rules | New files for `SpotifyCatalogueReleaseMatchStrategy`, `ExactReleaseMatchStrategy` | S | [x] |
| **P1** | `PodcastUpdater` scope/bypass | Extend `IndexingOrchestrationRules` — `ShouldEnrichDespiteReleaseWindow`, bypass flags | M | [x] |
| **P1** | Spotify/Apple enricher E2E | Mirror `YouTubeEpisodeEnricherCatalogueRules` pattern per platform | M | [x] |
| **P1** | Multi-platform orchestration (Apple/YouTube) | Extend `IndexingEnrichmentRules` — patch-applying mocks for Apple + YouTube | S | [x] |
| **P2** | `EpisodeIdentityExtensions` edge cases | Extend `PlatformIdentityMatchingRules` — Spotify URL ID extraction, Apple URL-only | S | [x] |
| **P2** | `PlatformLinkFactory` null guard | One adapter rule | S | [x] |
| **P2** | `AppleEpisodeEnricherTests` convention debt | Add `DisplayName`; use fixtures not raw `new Episode`/`new Podcast` | S | [x] |
| **P2** | Exact-title bypass extended matrix | Extend `CatalogueMatchingRules` (Phase D deferred) | M | [x] |
| **P3** | `YouTubePublishDelayMatchStrategy` lines 30–36 | Document dead code; test only if strategy order changes | S | [x] |
| **P3** | Raise matcher branch toward 90% aspiration | Incremental `CatalogueMatchingRules` | L | Open |
| **P3** | Add `PodcastServices.*.Tests` to coverage gate | `coverage-baseline.json` + `coverage-gate.ps1` — `platform-enrichment` group | M | [x] |
| **P3** | `PlaylistItemFinder` fuzzy/duration Theory expansion | `PlaylistItemFinderCatalogueWrapperRules` — raise branch toward aspiration | M | Open |

#### Phase F — deferred actions register (all open work)

Single register of **production cleanup** and **test/coverage aspiration** deferred past Phase E soak. Implement in Phase F PR(s) after #875 merges.

| # | Action | Type | Location / target | Priority | Blocker |
|---|--------|------|-------------------|----------|---------|
| F1 | Remove `EpisodeReleaseMatchTolerance` type + Abstractions call-site sweep | Cleanup | UrlSubmission categorisers, any remaining match-time callers | P1 | Tolerance parity rules green (#875) |
| F2 | Retire thin finder wrappers (`SearchResultFinder`, `PlaylistItemFinder`, Apple resolver wrapper) once call sites use domain matcher directly | Cleanup | `PodcastServices.Spotify/YouTube/Apple` finders | P1 | Soak confirms no finder quirk dependency |
| F3 | Unify UrlSubmission `EpisodeEnricher` onto `PlatformEnrichmentApplicator` (delete dual enrichment model) | Cleanup | `UrlSubmission/EpisodeEnricher.cs` | P1 | P0 parity rules green (#875); optional separate PR |
| F4 | Rename homonymous types (`SearchResultFinder` × platforms, legacy `EpisodeMatcher` surface) | Cleanup | Platform projects + DI | P2 | F2 or document retained wrappers |
| F5 | Sweep orchestrators for `switch (service)` / direct tolerance calls (§10.8) | Cleanup | `PodcastUpdater`, enrichers, categorisers | P2 | F1 |
| F6 | Extend `UrlSubmissionEnrichmentRules` — remaining `EpisodeEnricher` branches toward 85% aspiration | Test | `EpisodeEnricher.cs` (~43% branch) | P1 | Optional before F3 |
| F7 | `ResolvedAppleItemAdapter` URL-only negative branch rule | Test (optional) | `ResolvedAppleItemAdapter.cs` (50% branch) | P2 | — |
| F8 | YouTube `SearchResultFinder` wrapper rules (mirror Spotify) | Test | `SearchResultFinder.cs` | P3 | Before F2 if wrapper retained |
| F9 | `PlaylistItemFinder` fuzzy/duration Theory expansion | Test | `PlaylistItemFinderCatalogueWrapperRules` | P3 | Before F2 if wrapper retained |
| F10 | Raise `EpisodePlatformMatcher` branch toward 90% aspiration | Test | `CatalogueMatchingRules` | P3 | — |
| F11 | Template bypass/apply per Apple/YouTube enricher (optional E2E) | Test (optional) | Platform enrichers | P3 | — |
| F12 | Confirm discovery remains out of scope | Policy | `EpisodeResultsEnricher` | — | No action unless adapter-only already |

**Out of scope (document only):** Discovery / `EpisodeResultsEnricher`; publishing console apps unless DI touched by F4/F5.

#### Layer gaps to close with backlog above

| Gap | Risk |
|-----|------|
| Indexing uses `PlatformEnrichmentApplicator`; UrlSubmission mutates release/description in-place | P0 parity rules in `UrlSubmissionEnrichmentRules` prove equivalent release/description outcomes |
| `PlaylistItemFinder` has local matching logic; no wrapper rules (Spotify/Apple do) | `PlaylistItemFinderCatalogueWrapperRules` pins exact-title and publish-delay delegation |
| Legacy `EpisodeReleaseMatchTolerance` (Abstractions) still has live call sites | Phase F **F1** — removal after domain parity (#875) |
| `EpisodeEnricher` 43% branch, `PodcastUpdater` 68% branch | **F6** orchestration aspiration; `PodcastUpdater` scope/bypass characterized (#875) |

#### Cross-phase deferred (document only unless P0/P1 item covers)

| Item | Status |
|------|--------|
| Exact-title bypass beyond current matrix | [x] — `CatalogueMatchingRules` extended (Phase F P2, #875) |
| `YouTubePublishDelayMatchStrategy` lines 30–36 unreachable | [x] — documented in strategy + checklist (Phase F P3) |
| Discovery / `EpisodeResultsEnricher` | Out of scope |

### Checklist

- [ ] **F1** — Remove unused `EpisodeReleaseMatchTolerance` members / type (call-site sweep)
- [ ] **F2** — Retire transitional finder wrappers that only forward to domain matcher
- [ ] **F3** — Unify remaining `Resolved*Item` / catalogue mapping onto adapters (delete duplicate mappers); consider UrlSubmission → applicator
- [ ] **F4** — Rename confusing homonyms for clarity (document renames in PR)
- [ ] **F5** — Sweep for `switch (service)` or direct tolerance calls in orchestrators (§10.8 anti-patterns)
- [ ] **F12** — Confirm discovery remains out of scope (`EpisodeResultsEnricher` untouched unless already adapter-only)

### Exit criteria

- [ ] **Zero** business-rule assertion changes
- [ ] Full test set green; coverage gate passes
- [ ] No dead wrapper types left in the episode match/merge/apply/enrich path
- [ ] PR opened for Phase F only

### Risk to production

- **Risk level:** Low–Medium
- **Blast radius:** Potentially Indexer and UrlSubmission if DI renames or wrapper retirement miss a call site; should be mechanical
- **What changes live:** Naming, DI registration, deletion of dead wrappers/mappers — no intentional behavior change
- **What does not change:** Match, merge, apply, and enrich algorithms (already on domain from B–E)
- **Residual risks:**
  - Accidental behavior change when retiring a “thin” wrapper that still held a quirk
  - Missed call site after rename/DI cleanup causes runtime resolution failure
  - Discovery or console tools still depending on a deleted type (out of scope but may share assemblies)
- **Recommended soak / deploy scope:** Indexer + UrlSubmission if both composition roots change; otherwise the host(s) whose DI was touched
- **Rollback notes:** Revert cleanup commit; behavior path should be identical to post–Phase E

**PR:** _link_

---

## Cross-cutting (every PR)

Apply on **each** phase PR before merge:

### Deferred test gaps index (master)

Single index of test-gap status across phases. **Pre-soak backlog (Phase E P0–P3 + Phase F P0–P3) is complete in #875.** **Phase E Indexer soak in progress (2026-07-06).** Rows marked **Open** map to **Phase F deferred actions register** (F1–F12) — not Indexer soak blockers.

| Gap | Location | Address in phase | Priority | Status | Phase F ref |
|-----|----------|------------------|----------|--------|-------------|
| §5 catalog (57 rules) | README §5.1–§5.6 | Phases A–E | — | [x] Complete | — |
| Phase E enricher/applicator/template hardening | Episodes + PodcastServices.Tests | Phase E | P0–P3 | [x] Complete (#875) | — |
| Phase E Indexer soak | Indexer deployment | Phase E | — | 🟡 Soak in progress | Merge #875 after review |
| Pre-soak Phase F P0–P3 characterization | Multiple rule files | Phase F backlog (tests only, #875) | P0–P3 | [x] Complete (#875) | — |
| Phase F P0 UrlSubmission parity + BBC/IA | `UrlSubmissionEnrichmentRules` | Phase F P0 | P0 | [x] (#875) | F3 optional |
| Phase F P0 `PlaylistItemFinder` characterization | `PlaylistItemFinderCatalogueWrapperRules` | Phase F P0 | P0 | [x] (#875) | F2, F9 |
| `coverage-baseline.json` `platform-enrichment` group | 7 Phase E files + gate test projects | Phase E P3 | P3 | [x] (#875) | — |
| Tolerance parity / legacy `EpisodeReleaseMatchTolerance` removal | Abstractions + domain | Phase F cleanup | P1 | [x] Characterized — removal **Open** | **F1** |
| Release strategy direct rules | `ExactReleaseMatchStrategy`, `SpotifyCatalogueReleaseMatchStrategy` | Phase F P1 | P1 | [x] (#875) | — |
| `PodcastUpdater` scope/bypass branches | `PodcastUpdater.cs` | Phase F P1 | P1 | [x] (#875) | — |
| Spotify/Apple enricher E2E business rules | Platform enrichers | Phase F P1 | P1 | [x] (#875) | — |
| Apple/YouTube orchestration patch-applying mocks | `IndexingEnrichmentRules` | Phase F P1 | P1 | [x] (#875) | — |
| `EpisodeIdentityExtensions` edge cases | Domain extensions | Phase F P2 | P2 | [x] (#875) | — |
| `PlatformLinkFactory` null guard | Adapters | Phase F P2 | P2 | [x] (#875) | — |
| `AppleEpisodeEnricherTests` convention debt | Apple.Tests | Phase F P2 | P2 | [x] (#875) | — |
| Exact-title bypass extended matrix | `CatalogueMatchingRules` | Phase F P2 | P2 | [x] (#875) | — |
| `YouTubePublishDelayMatchStrategy` dead branches | Strategy file | Phase F P3 | P3 | [x] Documented (#875) | — |
| Remaining `EpisodeEnricher` branches (UrlSubmission) | `EpisodeEnricher.cs` (~43% branch) | Phase F | P1 | **Open** | **F6**, **F3** |
| `ResolvedAppleItemAdapter` optional negative branch | Adapters (50% branch) | Phase F P2 optional | P2 | **Open** | **F7** |
| Matcher branch aspiration (90%) | `EpisodePlatformMatcher.cs` (~79% gate) | Phase F P3 | P3 | **Open** | **F10** |
| `PlaylistItemFinder` branch aspiration (~44%) | Local fuzzy/duration in finder | Phase F P3 | P3 | **Open** | **F9**, **F2** |
| YouTube `SearchResultFinder` wrapper rules | YouTube search finder | Phase F P3 | P3 | **Open** | **F8**, **F2** |
| Template bypass per Apple/YouTube enricher (optional) | Platform enrichers | Phase F P3 optional | P3 | **Open** | **F11** |
| Phase F cleanup (wrapper/tolerance/unify enrich) | Phases D–E transitional code | Phase F PR | — | **Open** | **F1–F5** |
| Discovery / `EpisodeResultsEnricher` | Discovery hosts | Out of scope | — | N/A | **F12** |

See also: `coverage-baseline.json` → `gapsToClose` for coverage-specific follow-ups.

### Coverage gate

- [ ] `./scripts/coverage-gate.ps1` passes locally
- [ ] CI coverage job green (baseline in `coverage-baseline.json` — no regression)
- [ ] Do not lower baseline to land a phase; close gaps only by adding rules or covered paths

### `unit-tests.mdc` adherence

- [ ] No new tests that assert mocks-only or implementation method names
- [ ] New/adjusted tests (if any) use plain-English `DisplayName`, Arrange/Act/Assert, `EpisodeExpectation` or repository outcomes
- [ ] Lean arrange via fixtures/specimens; no hardcoded platform ID literals outside `DomainTestFixture.Incidents`

### No behavior change

- [ ] Diff contains **no** edits to existing business-rule assert lines (unless explicit signed-off behavior change PR — not a Step 7 phase PR)
- [ ] Incident-pin / rule catalog still describes **current** behavior
- [ ] SOLID placement respected (§10): adapters map, strategies match, policies merge, applier writes, orchestrators coordinate

### PR hygiene ([README §8](./README.md#8-agent--contributor-checklist))

- [ ] One phase only in the PR
- [ ] Applicable business rules already exist and stay green
- [ ] No new processing logic outside domain services / adapters / applier
- [ ] PR description links this checklist phase section
