# Step 8 — Post–Phase F test hardening plan

**Purpose:** Close coverage aspiration gaps and characterize new F17 boundaries after Phase F ([#876](https://github.com/cultpodcasts/RedditPodcastPoster/pull/876)). Includes **dead-code removal** where branches are provably unreachable.

**Prerequisites:** Phase F merged to `main`; coverage gate green on `main`.

**Related:** [STEP-7-CHECKLIST.md](./STEP-7-CHECKLIST.md) · [coverage-baseline.json](./coverage-baseline.json) · [architecture.md](../../Class-Libraries/RedditPodcastPoster.Episodes/architecture.md) · [unit-tests.mdc](../../.cursor/rules/unit-tests.mdc)

---

## Rules (every step)

| Rule | Detail |
|------|--------|
| **Tests-only PRs preferred** | No production logic changes except **verified dead-code deletion** (see §2) |
| **Business rules are the spec** | New tests in `*Rules.cs` with plain-English `DisplayName`, Arrange/Act/Assert |
| **Matrices for cross-products** | Use `[Theory]` + `MemberData` / `InlineData` per unit-tests.mdc §7 — not copy-paste Facts |
| **No baseline regression** | `./scripts/coverage-gate.ps1` must pass; raise floors in `coverage-baseline.json` only after measured gains |
| **No assertion changes** | Existing rule Then-clauses unchanged unless separate signed-off behavior PR |
| **Layer discipline** | Domain rules in `Episodes.Tests`; orchestration in `PodcastServices.Tests` / `UrlSubmission.Tests`; do not re-prove Layer 2 inside Layer 3 |

---

## Aspiration targets (from coverage-baseline.json)

| Group | Gate floor | Aspiration | Primary gap |
|-------|------------|------------|-------------|
| episodes-domain | 77% branch | **90%** | `EpisodeReleaseTolerance`, release strategies |
| episodes-adapters | 80% branch | 100% line | `PlatformLinkFactory`, `ResolvedAppleItemAdapter` |
| platform-enrichment | 66% branch | **85%** | Platform enrichers, enricher template |
| orchestration | 62% branch | **85%** | `CategorisedItemProcessor` (untested), `EpisodeEnricher`, `PodcastUpdater` |

---

## 1. Dead-code removal (do first)

Remove only when **provably unreachable** or **superseded** — characterize remaining behavior with tests before/after deletion in the same PR.

### 1.1 In scope — episode domain

| Item | Location | Evidence | Action |
|------|----------|----------|--------|
| **Unreachable Spotify→YouTube branch** | `YouTubePublishDelayMatchStrategy.cs` lines 30–38 | When `incomingIsYouTube`, line 23 (`!existingIsYouTube && incomingIsYouTube`) always runs first; lines 33–38 duplicate the same math with stricter guards but never execute. Documented in strategy + STEP-7 P3. | **Delete** lines 30–38 and unreachable comment block. **Keep** `YouTubePublishDelayMatchStrategyRules.positive_delay_spotify_stored_youtube_incoming_aligned` — it exercises line 23–27, not the dead block. |
| **Duplicate tolerance tests (tech debt)** | `PodcastServices.Apple.Tests/EpisodeReleaseToleranceTests.cs` | Overlaps `EpisodeReleaseToleranceRules`; violates unit-tests.mdc (no DisplayName, raw `new Episode`, fixed dates). | **Migrate** unique scenarios into `Episodes.Tests/.../EpisodeReleaseToleranceRules.cs`, then **delete** the Apple file (or reduce to a single smoke if needed for Apple project compile — prefer deletion). |

### 1.2 Out of scope (separate issue/PR)

| Item | Location | Notes |
|------|----------|-------|
| ~~`FindChannel` stub + unreachable body~~ | `YouTubeChannelService.cs` | Removed — zero callers; channel search lives on `YouTubeChannelResolver.FindChannelsSnippets`. | **Done** |
| Finder wrappers (F2) | `SpotifySearchResultFinder`, `PlaylistItemFinder`, etc. | **Retain** — not dead; platform heuristics. |
| `PlatformEpisodeEnricherTemplate` in Abstractions | Abstractions → Episodes ref | Accepted coupling; not dead code. |

### 1.3 Dead-code PR checklist

- [x] Run full test suite before deletion
- [x] Delete dead branch; run `YouTubePublishDelayMatchStrategyRules` — all green
- [ ] Confirm coverage on `YouTubePublishDelayMatchStrategy.cs` **increases** (branch % should rise after removing unreachable lines)
- [x] Update `coverage-baseline.json` note for that file (remove “unreachable lines 30–36” wording)
- [x] No change to matcher strategy **registration order** in `AddEpisodesDomain()`

---

## 2. Implementation phases

One PR per phase (or pair P0+P1 if small). Order matters: dead code → F17 boundary → orchestration gaps → aspiration matrices.

### Phase 8A — Dead code + F17 boundary (P0)

**Goal:** Pin F17 contract; remove proven dead strategy branch.

| Task | Test file (new or extend) | Matrix / rules |
|------|---------------------------|----------------|
| **8A.1** Remove `YouTubePublishDelayMatchStrategy` lines 30–38 | — (production) | Verify existing `YouTubePublishDelayMatchStrategyRules` still green |
| **8A.2** `PlatformResolvedItemMappers.FromPlatform` field parity | `UrlSubmission.Tests/BusinessRules/UrlSubmission/UrlSubmissionCategorisationRules.cs` | `[Theory]` × 3 platforms: every DTO field maps from platform resolved type |
| **8A.3** `Categorised*Item.ToAdapterInput()` | same file | `[Theory]` × 3 platforms: adapter input carries enrich-relevant fields; YouTube includes playlist semantics where applicable |
| **8A.4** Round-trip invariant | same file | `FromPlatform(platformItem)` → DTO → `ToAdapterInput()` → resolved adapter produces same `EpisodeCandidate` as direct platform adapter (spot-check per platform) |

**Production touch:** `YouTubePublishDelayMatchStrategy.cs` only.

**Exit:** UrlSubmission.Tests + Episodes.Tests green; no new platform-type references in orchestration tests beyond categoriser boundary.

---

### Phase 8B — UrlSubmission orchestration (P0)

**Goal:** Close orchestration aspiration gap for `CategorisedItemProcessor` and residual `EpisodeEnricher` branches.

**Note:** `UrlSubmissionPersistenceRules` and `UrlSubmissionEnrichmentRules` already had extensive coverage when Step 8 started; plan “zero tests” claim was stale.

| Task | Test file | Matrix |
|------|-----------|--------|
| **8B.1** Existing podcast path | `UrlSubmissionPersistenceRules.cs` (extend) | ✅ existing + added persist-false + podcast+episode both enriched |
| **8B.2** New podcast path | same | ✅ covered |
| **8B.3** Podcast-only enrich save | same | ✅ covered |
| **8B.4** `EpisodeEnricher` residual branches | `UrlSubmissionEnrichmentRules.cs` | ✅ extensive single-platform + non-podcast rules already present |

**Exit:** `CategorisedItemProcessor` branch ≥ 85%; `EpisodeEnricher` branch ≥ 80% (update baseline after measure).

---

### Phase 8C — `EpisodeReleaseTolerance` consolidation (P1)

**Goal:** Raise domain branch from **54%** toward **75%+**; single canonical rule file in Episodes.Tests.

| Task | Test file | Matrix (`MemberData`) |
|------|-----------|------------------------|
| **8C.1** `GetToleranceTicks` overload (null podcast) | `EpisodeReleaseToleranceRules.cs` | ✅ `NullPodcastToleranceScenarioNames` mirrors podcast overload |
| **8C.2** `GetAudioReleaseForPlatformLookup` | same | ✅ `AudioReleaseLookupScenarioNames` + merged YT/Spotify case |
| **8C.3** `ShouldEnrichDespiteReleaseWindow` | same | ✅ positive + 4 negative scenarios |
| **8C.4** `ShouldPreserveYouTubeAuthoritativeRelease` | same | ✅ YT identity present/absent |
| **8C.5** Migrate + delete | ✅ Removed `Apple.Tests/EpisodeReleaseToleranceTests.cs` | Matcher-only cases retained in Episodes.Tests strategy/merge rules |

**Note:** `ShouldEnrichDespiteReleaseWindow` uses `DateTime.UtcNow` in production — tests may use fixture dates near window edges; document if time-sensitive tests need frozen clock (only if flaky).

**Exit:** `EpisodeReleaseTolerance.cs` branch ≥ 70%; Apple.Tests no duplicate tolerance file.

---

### Phase 8D — Release strategies + domain extensions (P1)

| Task | Test file | Matrix |
|------|-----------|--------|
| **8D.1** `SpotifyCatalogueReleaseMatchStrategy` (50% branch) | `SpotifyCatalogueReleaseMatchStrategyRules.cs` | positive-delay YT authority; Apple-incoming defer; edge null-defer rows |
| **8D.2** `ExactReleaseMatchStrategy` (75% branch) | `ExactReleaseMatchStrategyRules.cs` | release delta within/outside tolerance × delay zero/negative/positive |
| **8D.3** `EpisodeMappingExtensions` (78% branch) | `EpisodeMappingExtensionsRules.cs` | `[MemberData(nameof(AllPlatformServices))]` for `ToCandidate` / patch builders |
| **8D.4** `EpisodeIdentityExtensions` (81% branch) | new `EpisodeIdentityExtensionsRules.cs` | Spotify URL ID extraction × Apple URL-only identity |
| **8D.5** `EpisodePlatformApplier` (81% branch) | `EpisodePlatformApplierRules.cs` | extend `AllPlatformServices` matrix for fill-missing vs preserve-existing per field |

**Exit:** episodes-domain group branch measurably closer to 90% aspiration; update `coverage-baseline.json` per-file notes.

---

### Phase 8E — Adapters (P2)

| Task | Test file | Matrix |
|------|-----------|--------|
| **8E.1** `PlatformLinkFactory` (50% branch) | `PlatformCatalogueAdapterRules.cs` or new `PlatformLinkFactoryRules.cs` | partial inputs: id-only, url-only, image-only, all-null (exists) |
| **8E.2** `ResolvedAppleItemAdapter` (50% branch) | `ResolvedItemAdapterRules.cs` | URL-only vs id+url × empty description |

**Exit:** episodes-adapters line aspiration 100% maintained; branch floors raised.

---

### Phase 8F — Platform enrichment E2E (P2)

**Goal:** platform-enrichment branch **66% → 80%+**.

| Task | Test file | Matrix |
|------|-----------|--------|
| **8F.1** `PlatformEpisodeEnricherTemplate` (50% branch) | `PlatformEpisodeEnricherTemplateRules.cs` | bypass delayed publishing × apply resolved candidate × null catalogue |
| **8F.2** `PlatformEnrichmentResultExtensions` (78% branch) | `PlatformEnrichmentResultExtensionsRules.cs` | extend `PlatformUrlServices` matrix for release + URL flags |
| **8F.3** `AppleEpisodeEnricher` (50% branch) | `AppleEpisodeEnricherCatalogueRules.cs` | catalogue match / no match / bypass template |
| **8F.4** `YouTubeEpisodeEnricher` (65% branch) | `YouTubeEpisodeEnricherCatalogueRules.cs` | Enrich entry paths; link-only applier backfill |
| **8F.5** `SpotifyEpisodeEnricher` (66% branch) | `SpotifyEpisodeEnricherCatalogueRules.cs` | full catalogue flow; expensive-query side effect interaction |
| **8F.6** `PlaylistItemFinder` (55% branch) | `PlaylistItemFinderCatalogueWrapperRules.cs` | extend fuzzy `TheoryData` if still below 60% after 8A–8E |

**Pattern:** `platform × { match, no match, bypass, link-only }` — reuse `DelayedPublishingAudioPlatforms` style from `IndexingEnrichmentRules`.

**Exit:** platform-enrichment branch ≥ 75% (stretch 85%).

---

### Phase 8G — Indexing orchestration (P2)

| Task | Test file | Matrix |
|------|-----------|--------|
| **8G.1** `PodcastUpdater` (68% branch) | `IndexingOrchestrationRules.cs` | enrich-only scope × `ShouldEnrichDespiteReleaseWindow` × bypass flags |
| **8G.2** Indexing + tolerance integration | `IndexingScopeRules.cs` | cross-platform second-pass delayed YouTube publishing |

**Exit:** orchestration branch ≥ 82% (stretch 85%).

---

## 3. Matrix catalog (reuse across phases)

Standard `MemberData` sources — add to `DomainTestFixture` or test class static helpers, not duplicated per file.

| Name | Values | Used for |
|------|--------|----------|
| `AllPlatformServices` | Spotify, Apple, YouTube | Applier, mapping, enrichment |
| `AllCategorisedPlatforms` | Spotify, Apple, YouTube | F17 mappers, UrlSubmission DTOs |
| `DelaySign` | zero, negative, positive | Tolerance, strategies |
| `ReleaseAuthority` | default, Spotify-primary, YouTube | Tolerance, strategies |
| `FuzzyTitleVariantStrategy` | ReplaceWord, DropWord, AddFillerWord, SwapAdjacentWords | Finders (existing) |
| `PersistToDatabase` | true, false | CategorisedItemProcessor |
| `SubmitResultStates` | Created, Enriched, unchanged/skipped | UrlSubmission persistence |

---

## 4. PR sequence (recommended)

| PR | Phases | Production changes |
|----|--------|-------------------|
| **#1** | 8A | `YouTubePublishDelayMatchStrategy` dead branch removal + F17 rules |
| **#2** | 8B | Tests only |
| **#3** | 8C | Tests only + delete `Apple.Tests/EpisodeReleaseToleranceTests.cs` |
| **#4** | 8D + 8E | Tests only |
| **#5** | 8F + 8G | Tests only |

After each PR: run `./scripts/coverage-gate.ps1`; update `coverage-baseline.json` `gapsToClose` and per-file `branchCoverage` when floors rise.

---

## 5. Exit criteria (Step 8 complete)

- [x] Dead branch removed from `YouTubePublishDelayMatchStrategy`; strategy rules green
- [x] F17 categorisation characterized in `UrlSubmissionCategorisationRules`
- [x] `CategorisedItemProcessor` has persistence business rules
- [x] `EpisodeReleaseTolerance` single home in Episodes.Tests; Apple duplicate removed
- [ ] `coverage-baseline.json` `gapsToClose` updated — no stale “unreachable lines 30–36” entry
- [ ] Coverage gate green; group aspirations met or documented as accepted residual:
  - episodes-domain branch ≥ **85%** (90% stretch)
  - platform-enrichment branch ≥ **75%** (85% stretch)
  - orchestration branch ≥ **82%** (85% stretch)
- [ ] No existing business-rule assertion lines changed without sign-off
- [ ] STEP-7 checklist gets a “Step 8” row linking this plan (when work starts)

---

## 6. Explicit non-goals

- Changing matcher strategy registration order to “activate” removed dead code
- Testing FuzzySharp scores as business rules
- Discovery `EpisodeResultsEnricher` pipeline
- ~~`YouTubeChannelService.FindChannel` stub cleanup~~ (removed — no callers)
- Lowering coverage baselines to land tests
