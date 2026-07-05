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
| **D** — Collapse finders into single matcher | ⬜ Not started | Medium–High | _PR link_ |
| **E** — Shared enricher template | ⬜ Not started | Medium | _PR link_ |
| **F** — Cleanup | ⬜ Not started | Low–Medium | _PR link_ |

---

## Phase 0 / Phase A status

Steps 1–6 are complete. **Phase A** is in production (merged via [PR #871](https://github.com/cultpodcasts/RedditPodcastPoster/pull/871)). **Phase B** is in production (merged via [PR #872](https://github.com/cultpodcasts/RedditPodcastPoster/pull/872)). **Phase C** in production (soak, review pending — [PR #873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873)). Phases D–F not started.

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

### Phase C deploy / soak status

- [x] Branch deployed from [PR #873](https://github.com/cultpodcasts/RedditPodcastPoster/pull/873) (2026-07-05)
- [x] **Indexer** — catalogue providers → adapters → factory (`SpotifyEpisodeAdapter`, `AppleEpisodeAdapter`, `YouTubeEpisodeAdapter` at provider/resolver boundaries)
- [ ] Soak review pending

### Risk to production (Phase C — live / soak context)

- **Risk level:** Medium–High
- **Blast radius:** Indexer (all platform providers/resolvers); any host that builds episodes from catalogue API types. Discovery remains out of scope unless a shared provider is touched accidentally
- **What changes live:** Spotify/Apple/YouTube catalogue → `EpisodeCandidate` at the boundary; `Episode.From*` / raw API types leave the indexing processing path
- **What does not change:** Finder scoring logic (still Phase D); enricher mutation patterns (still Phase E); UrlSubmission already on adapters from Phase B; Api / Discovery unchanged by Phase C deploy
- **Residual risks:**
  - Adapter mapping quirks (IDs, URLs, release shapes) affect every indexed episode for all platforms
  - Scattered `Episode.From*` leftovers produce divergent episode shapes if not fully removed
  - Platform-specific flags (e.g. Spotify expensive-query) misplaced into matcher/merger
- **Soak / deploy scope:** Indexer only (deployed 2026-07-05; soak review pending)
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

- [ ] Phase C merged (candidates available at boundaries)
- [x] Matcher rule catalog (§5.1) green against `EpisodePlatformMatcher`

### Checklist

- [ ] Inventory all finder/match entry points that duplicate identity, title/duration, or release tolerance logic
- [ ] Route Spotify finder matching through `IEpisodePlatformMatcher` (candidates from adapters)
- [ ] Route YouTube search/playlist finder matching through `IEpisodePlatformMatcher`
- [ ] Route Apple match paths through `IEpisodePlatformMatcher`
- [ ] Move remaining `EpisodeReleaseMatchTolerance` **match-time** methods into `IReleaseMatchStrategy` classes without semantic changes (§10.9)
- [ ] Leave finders as thin wrappers (resolve candidates + call matcher) or delete if fully superseded
- [ ] Document strategy registration order if DI order changes (first applicable wins)

### Exit criteria

- [ ] Matcher business-rule tests pass **without assertion changes**
- [ ] Duplicate finder scoring/tolerance code removed (no parallel implementations left)
- [ ] Orchestration/indexing rules still pass **without assertion changes**
- [ ] `./scripts/coverage-gate.ps1` passes
- [ ] PR opened for Phase D only

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

**PR:** _link_

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

- [ ] Phase D merged (matching is domain-owned)
- [x] Indexing enrichment rules (§5.4) green

### Checklist

- [ ] Introduce shared enrich flow (template/base) that: resolve → adapt → build `EpisodePlatformPatch` → apply via `IEpisodePlatformApplier`
- [ ] Refactor Spotify enricher to return/apply patch only (no in-place `Episode` mutation for platform fields)
- [ ] Refactor Apple enricher the same way
- [ ] Refactor YouTube enricher the same way; honor `SkipEnrichingFromYouTube` in orchestration only
- [ ] Keep delayed-publishing second pass and batch exclusion in orchestrator (not in platform enrichers)
- [ ] Extract Spotify expensive-query persistence to a narrow side-effect if still coupled to enricher mutation
- [ ] Update `PodcastServicesEpisodeEnricherTestSupport` / mocks to return patches or apply via real applier

### Exit criteria

- [ ] Enrichment business-rule tests pass **without assertion changes**
- [ ] Indexing orchestration/persistence rules pass **without assertion changes**
- [ ] No enricher path mutates platform fields without going through applier
- [ ] `./scripts/coverage-gate.ps1` passes
- [ ] PR opened for Phase E only

### Risk to production

- **Risk level:** Medium
- **Blast radius:** Indexer — `PodcastServicesEpisodeEnricher` and platform enrichers (Spotify, Apple, YouTube)
- **What changes live:** Shared enrich template (resolve → adapt → patch → applier); platform enrichers no longer mutate flat `Episode` platform fields in place
- **What does not change:** Match logic (Phase D); UrlSubmission enrich path (Phase B); discovery enrichers (out of scope)
- **Residual risks:**
  - `SkipEnrichingFromYouTube` or delayed-publishing second pass moved into enrichers by mistake
  - Spotify expensive-query side-effect lost or double-applied
  - Patch apply order differs from prior in-place mutation for multi-platform enrich
- **Recommended soak / deploy scope:** Indexer only
- **Rollback notes:** Restore platform enrichers’ direct mutation; shared template can be removed without touching matcher/merger

**PR:** _link_

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

- [ ] Phases B–E merged
- [ ] No production call sites depend on retired wrappers

### Checklist

- [ ] Unify any remaining `Resolved*Item` / catalogue mapping onto adapters (delete duplicate mappers)
- [ ] Rename confusing homonyms for clarity (document renames in PR)
- [ ] Retire transitional wrappers that only forward to domain services
- [ ] Remove unused `EpisodeReleaseMatchTolerance` members / type if fully migrated
- [ ] Confirm discovery remains out of scope (`EpisodeResultsEnricher` untouched unless already adapter-only)
- [ ] Sweep for `switch (service)` or direct tolerance calls in orchestrators (§10.8 anti-patterns)

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
