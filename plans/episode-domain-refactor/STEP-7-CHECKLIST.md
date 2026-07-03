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

| Phase | Status | PR |
|-------|--------|----|
| **A** — Domain types + applier/merger/matcher (internal) | ✅ Done | (via Steps 2–3 / EpisodeMerger wiring) |
| **B** — UrlSubmission through applier | ⬜ Not started | _PR link_ |
| **C** — Platform adapters at boundaries | ⬜ Not started | _PR link_ |
| **D** — Collapse finders into single matcher | ⬜ Not started | _PR link_ |
| **E** — Shared enricher template | ⬜ Not started | _PR link_ |
| **F** — Cleanup | ⬜ Not started | _PR link_ |

---

## Phase 0 status (done — do not re-open)

Steps 1–6 and **Phase A** are complete. Brief record only:

| Step / phase | Outcome |
|--------------|---------|
| Steps 1–6 | Test infrastructure, domain types, Layer 1–3 business rules, adapter rules, coverage baseline + CI gate |
| **Phase A** | Domain services in `RedditPodcastPoster.Episodes`; `EpisodeMerger` / `EpisodeMatcher` wired to `EpisodePlatformMatcher` / `EpisodePlatformMerger` |
| Adapters | Exist under `Episodes/Adapters/` with Layer 1 rules — **not** wired at provider/resolver boundaries |
| Applier | Used inside Episodes (via merger path) — **not** used by UrlSubmission `EpisodeEnricher` |

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

- [ ] Inject `IEpisodePlatformApplier` (and adapters as needed) into `EpisodeEnricher`
- [ ] Map each `Resolved*Item` on `CategorisedItem` through the corresponding adapter → candidate/patch
- [ ] Apply missing platform links (ID, URL, image) via applier — no direct `matchingEpisode.SpotifyId = …` style writes for platform fields
- [ ] Preserve podcast-level enrichment (show IDs, etc.) and non-platform episode fields (description helper, BBC/IA if still special-cased) without regressing rules
- [ ] Update UrlSubmission test construction to supply applier (real implementation, not a mock that hides behavior)
- [ ] Confirm no new processing logic outside adapters / applier for platform field writes

### Exit criteria

- [ ] All UrlSubmission business-rule tests pass **without assertion changes**
- [ ] Full Step 7 test set green (Episodes, PodcastServices, UrlSubmission, Persistence)
- [ ] `./scripts/coverage-gate.ps1` passes (no regression below baseline)
- [ ] PR opened for Phase B only

**PR:** _link_

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
- [ ] Phase B merged (UrlSubmission path already uses adapters/applier pattern)

### Checklist

- [ ] Wire Spotify catalogue path: API/`FullEpisode` (or existing input DTO) → `SpotifyEpisodeAdapter` → `EpisodeCandidate` at provider/resolver boundary
- [ ] Wire Apple catalogue path: `AppleEpisode` (or input DTO) → `AppleEpisodeAdapter` → `EpisodeCandidate`
- [ ] Wire YouTube catalogue path: `SearchResult` / `PlaylistItem` (or input DTOs) → `YouTubeEpisodeAdapter` → `EpisodeCandidate`
- [ ] Convert candidates to persisted `Episode` only via applier/merger (or a single documented factory), not scattered `Episode.From*` in orchestration
- [ ] Keep platform-specific flags at the boundary (`Spotify` expensive-query, etc.) — do not push into matcher/merger
- [ ] Update any provider/resolver unit tests to assert candidate/episode outcomes, not adapter internals alone

### Exit criteria

- [ ] Adapter business-rule tests pass **without assertion changes**
- [ ] Matching/merging/indexing/UrlSubmission rules still pass **without assertion changes**
- [ ] `./scripts/coverage-gate.ps1` passes
- [ ] PR opened for Phase C only

**PR:** _link_

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
