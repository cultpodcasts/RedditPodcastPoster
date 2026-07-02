# Episode domain refactor — plan, guardrails, and test spec

> **Status:** Pre-refactor — tests must land before implementation changes.  
> **Scope:** Indexing (`PodcastUpdater` pipeline) and UrlSubmission episode handling.  
> **Out of scope (initially):** Discovery `EpisodeResultsEnricher`, console backfill tools.

---

## 1. Purpose

Episode finding, merging, and enrichment logic is duplicated across platform services (Spotify, Apple, YouTube), persistence (`EpisodeMerger`), and UrlSubmission (`EpisodeEnricher`). Foreign API types leak into processing code, producing subtle behavioral differences and bugs.

This refactor introduces **domain types at platform boundaries** and **shared domain services** for match, merge, and apply. The refactor must not change behavior unless explicitly agreed and tested.

---

## 2. Non-negotiables

| Rule | Rationale |
|------|-----------|
| **Tests before refactor code** | No domain extraction PR merges without its rule tests |
| **Business-rule tests are the spec** | Plain-English rules + Given/When/Then; not implementation-focused names |
| **No behavior change by accident** | Changing a test assertion requires explicit sign-off |
| **Do not trust existing merge tests as comprehensive** | Recent incident-pin tests (~PRs #866–870) are starting points, not a safety net |
| **Assert outcomes, not internals** | Repository commits and `EpisodeExpectation` snapshots; not “mock was called” alone |
| **Characterize current behavior including quirks** | Suspected bugs get a rule documenting today’s behavior; fixes are separate PRs |

---

## 3. Refactor destination

### 3.1 Problem today

Four parallel representations of “episode from a platform”:

| Representation | Where | Used by |
|----------------|-------|---------|
| Flat `Episode` | `Models/Episode.cs` | Persistence, indexing, UrlSubmission |
| Platform API types | Spotify `FullEpisode`, `AppleEpisode`, YouTube `SearchResult`/`PlaylistItem` | Resolvers, providers, enrichers |
| `Resolved*Item` | UrlSubmission categorisation | `EpisodeEnricher` |
| `EpisodeResult` | Discovery | Discovery enrichers |

Processing logic (match, merge, fill-missing fields) is copy-pasted across these paths with divergent details.

### 3.2 Target architecture

```
Platform API / Resolved*Item / EpisodeResult
        │
        ▼  (thin adapter — map once)
   EpisodeCandidate  +  PlatformLink  +  ReleaseInfo
        │
        ├──► EpisodePlatformMatcher  +  IReleaseMatchStrategy (per platform)
        ├──► EpisodePlatformMerger
        └──► EpisodePlatformApplier  ──►  Episode  ──►  repository
```

**Orchestration stays thin:** `PodcastUpdater`, `PodcastServicesEpisodeEnricher`, `CategorisedItemProcessor` coordinate; they do not embed platform matching rules.

### 3.3 New domain types

New project: **`RedditPodcastPoster.Episodes`** (or under `Models` if preferred).

```csharp
// Canonical interchange type inside episode processing — not persisted directly
record EpisodeCandidate(
    string Title,
    string Description,
    TimeSpan Duration,
    ReleaseInfo Release,
    PlatformLink? SourceLink);  // link for the platform this candidate came from

record PlatformLink(
    Service Service,
    string? Id,
    Uri? Url,
    Uri? Image);

record ReleaseInfo(
    DateTime Value,
    ReleasePrecision Precision);

enum ReleasePrecision { DateOnly, DateTimeUtc }

// Structured delta applied to Episode — replaces ad-hoc EnrichmentContext booleans at domain boundary
record EpisodePlatformPatch(
    PlatformLink? Link,
    string? Description,
    ReleaseInfo? Release);
```

**`Episode` (persisted model) unchanged** for Cosmos schema. Domain types sit at service boundaries; `EpisodePlatformApplier` is the single place that writes flat fields (`SpotifyId`, `Urls.Spotify`, `Images.Spotify`, etc.).

### 3.4 Domain services

| Service | Responsibility | Platform-specific part |
|---------|---------------|------------------------|
| `EpisodePlatformMatcher` | Find best match among candidates | Delegates release comparison to `IReleaseMatchStrategy` |
| `EpisodePlatformMerger` | Fill-missing merge of links, description, release | Release merge rules (Apple/YouTube time backfill; Spotify date-only) |
| `EpisodePlatformApplier` | Apply `EpisodePlatformPatch` → `Episode` | None |
| `IReleaseMatchStrategy` | Compare releases for match tolerance | `SpotifyReleaseMatchStrategy`, `YouTubeDelayedReleaseMatchStrategy`, `AudioReleaseMatchStrategy` |
| Platform adapters | `FullEpisode` / `AppleEpisode` / `SearchResult` / `Resolved*Item` → `EpisodeCandidate` | One adapter per platform + UrlSubmission |

### 3.5 What stays specialized

These are **not** over-unified:

- **Spotify release is date-only** — `ReleasePrecision.DateOnly`; no time-of-day backfill from Spotify
- **YouTube publish delay** — positive and negative delay; go-live checks for negative delay
- **Apple time-of-day** — may upgrade midnight UTC release on same calendar date
- **Spotify expensive-query flag** — remains in Spotify adapter/enricher
- **YouTube `SkipEnrichingFromYouTube`** — remains orchestration concern

Existing `EpisodeReleaseMatchTolerance` logic migrates into `IReleaseMatchStrategy` implementations; do not rewrite semantics during extraction.

### 3.6 Implementation phases (after test gate)

| Phase | PR | Changes | Test impact |
|-------|-----|---------|-------------|
| **A** | Introduce domain types + applier/merger/matcher (used internally) | New types in `RedditPodcastPoster.Episodes`; wire `EpisodeMerger` to domain services | Domain rule tests pass; existing tests pass |
| **B** | UrlSubmission through applier | `EpisodeEnricher` uses `EpisodePlatformApplier` | UrlSubmission rule tests pass |
| **C** | Platform adapters | Providers/resolvers map to `EpisodeCandidate` at boundary | Adapter rule tests pass |
| **D** | Collapse finders | Single `EpisodePlatformMatcher`; thin YouTube/Spotify/Apple wrappers | Matcher rule tests pass; delete duplicate finder code |
| **E** | Enricher template | Shared enrich flow; platform enrichers supply adapter + resolver | Enrichment rule tests pass |
| **F** | Cleanup | Unify `Resolved*Item` mappers; rename homonyms; retire wrappers | No assertion changes |

**Each phase:** zero changes to business-rule test assertions.

---

## 4. Test implementation guardrails

### 4.1 Three test layers

```
Layer 2 — Domain business rules     ← PRIMARY INVESTMENT (spec for refactor)
Layer 3 — Orchestration rules       ← persistence, routing, save order
Layer 1 — Adapter rules             ← platform mapping quirks
```

Do **not** write Layer 3 tests that re-prove Layer 2 matching/merging logic. Orchestration tests mock at enricher/provider boundary and assert repository outcomes.

### 4.2 Business-rule test format (mandatory)

```csharp
[Fact(DisplayName =
    "Plain English rule: when X, then Y, because Z.")]
public void snake_case_method_name()
{
    // Given <business setup>
    // When <action>
    // Then <observable outcome>
}
```

**Requirements:**

- **`DisplayName`** is the authoritative rule text (readable in Test Explorer / CI)
- Method name is snake_case shorthand; never the only documentation
- Body uses **`// Given` / `// When` / `// Then`** comments in every test
- **`EpisodeExpectation`** (or equivalent) for Then-clauses — not fifteen separate property assertions unless the rule is about one field
- **Seed data from production incidents** is encouraged (C2C, Postmormon, Spotify URL-only) but must be expressed as rules with full outcome assertions

**Do not:**

- Test third-party libraries (e.g. FuzzySharp scores) as business rules
- Assert only `MergedEpisodes.Should().ContainSingle()` without stating what merged
- Name tests after implementation (`MergeInPlace_WhenUrlsSpotifyNull`)
- Mock so heavily that the Then-clause proves nothing about episode state

### 4.3 `EpisodeExpectation` (test vocabulary)

Define in `RedditPodcastPoster.Episodes.Tests` (or shared test project):

```csharp
record EpisodeExpectation(
    PlatformExpectation? Spotify,
    PlatformExpectation? Apple,
    PlatformExpectation? YouTube,
    DateTime Release,
    string? Description,
    bool Ignored = false,
    bool Removed = false);

record PlatformExpectation(string? Id, Uri? Url, Uri? Image);

static EpisodeExpectation From(Episode episode);
static EpisodeExpectation From(EpisodeCandidate candidate);  // after domain types exist
```

Same helper works before and after refactor — tests stay stable.

### 4.4 Test infrastructure (mandatory before rule tests)

| Component | Purpose |
|-----------|---------|
| `InMemoryEpisodeRepository` | Seed + capture saves; support `GetByPodcastId` predicates |
| `InMemoryPodcastRepository` | Seed + capture podcast saves |
| `PodcastFixtures` | `YouTubeFirst()`, `SpotifyPrimary()`, `ApplePrimary()`, negative delay |
| `EpisodeFixtures` | Incident-based builders; `SubmittedViaSpotifyUrlOnly()`, etc. |
| `SaveCallRecorder` | Assert save order for indexing (enriched → filtered → merged → added) |

### 4.5 Mock boundaries

| Layer | Mock at | Real implementation |
|-------|---------|---------------------|
| Domain (Layer 2) | Nothing | Matcher, merger, applier, release strategies |
| Orchestration (Layer 3) | `IEpisodeProvider`, `I*EpisodeEnricher`, `IPodcastFilter` | Domain services (once wired) or real merger during transition |
| UrlSubmission (Layer 3) | `IUrlCategoriser` optional; build `CategorisedItem` directly | `CategorisedItemProcessor` + applier |
| Adapters (Layer 1) | Nothing | Adapter class only |

Platform enricher mocks should return **`EpisodePlatformPatch`** (once types exist) or apply a documented patch via applier — not arbitrary in-place mutation without asserting resulting state.

### 4.6 Coverage (secondary gate)

Coverage does **not** prove rule completeness. Use it to find **missed branches** after the rule catalog is implemented.

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults/coverage
```

| Target | Branch coverage goal |
|--------|---------------------|
| `RedditPodcastPoster.Episodes` domain services | ≥ 90% |
| Orchestration files (`PodcastUpdater`, `CategorisedItemProcessor`) | ≥ 85% |
| Platform adapters | ~100% line (small files) |

Baseline per-file after Phase 0; CI fails if orchestration/domain files drop below baseline on PR.

### 4.7 Handling suspected bugs

If code behavior looks wrong:

1. Write a rule describing **current** behavior: `"When BBC URL is submitted, image is stored on Images.YouTube (current behavior)"`
2. Mark with comment `// KNOWN: likely bug — fix tracked separately`
3. Do **not** fix during refactor PR

---

## 5. Business rules catalog

Implement as test classes under `BusinessRules/`. Each rule = one `[Fact]` or `[Theory]` with DisplayName.

### 5.1 Matching (`BusinessRules/Matching/`)

**Platform identity**

- Episodes with the same Spotify ID are the same episode.
- Episodes with the same Spotify URL are the same episode, even when Spotify ID is not yet stored.
- Episodes with different Spotify IDs are never merged, even when titles are identical.
- Episodes with the same YouTube video ID are the same episode.
- Episodes with different YouTube video IDs are never merged.
- Episodes with the same Apple episode ID are the same episode.
- Episodes with different Apple episode IDs are never merged.
- When an incoming platform ID is already assigned to a different stored episode, do not merge onto the wrong row.

**Title and duration**

- When titles differ by a typo but duration matches within tolerance, episodes may be treated as the same.
- When titles differ by a typo but duration does not match, episodes are not the same.
- When titles and duration differ but release and duration align within standard tolerance, episodes may be treated as the same (non-YouTube-first).
- Custom `EpisodeMatchRegex` on the podcast may force a match when titles differ.

**Cross-platform (YouTube-first)**

- For YouTube-first podcasts, a Spotify catalogue episode may match a YouTube-only stored episode when title/duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.
- For YouTube-first podcasts with negative publishing delay, episodes must not merge on release-and-duration alone when titles clearly refer to different episodes.
- When two stored episodes could both match an incoming episode, indexing must record merge failure — not pick arbitrarily.

### 5.2 Release dates (`BusinessRules/Merging/ReleaseDateMergingRules.cs`)

- Spotify catalogue release is date-only: time-of-day from Spotify must not overwrite a stored release.
- When stored release is midnight UTC and YouTube provides a time on the same calendar date, backfill the time from YouTube.
- When stored release is midnight UTC and Spotify provides a time on the same calendar date, do not backfill.
- When stored release is midnight UTC and YouTube provides a time on a different calendar date, do not backfill.
- For YouTube-authority podcasts, re-indexing from Spotify must not replace the YouTube publish datetime with a newer Spotify catalogue date.
- Apple may upgrade a date-only stored release to a full datetime when the calendar date matches.

### 5.3 Merging fields (`BusinessRules/Merging/EpisodeMergingRules.cs`)

- Merge fills missing Spotify, Apple, and YouTube URLs; it does not replace existing URLs.
- Merge fills missing platform IDs; it does not overwrite an existing ID with a different one.
- Merge fills missing artwork per platform; it does not replace existing artwork.
- Merge may replace a truncated description (ending in `...`) with a longer description; it does not replace a complete description with a shorter one.
- A discovered episode with no match is added as a new row with a new ID.

### 5.4 Indexing enrichment (`BusinessRules/Enrichment/IndexingEnrichmentRules.cs`)

- When Spotify URL or ID is missing, indexing attempts Spotify enrichment.
- When Apple URL or ID is missing, indexing attempts Apple enrichment.
- When YouTube URL or ID is missing and the podcast has a YouTube channel, indexing attempts YouTube enrichment.
- When `SkipEnrichingFromYouTube` is true, YouTube enrichment is not attempted.
- For podcasts with positive YouTube publishing delay, a second pass enriches recently expired delayed-publishing episodes that were not part of the current discovery batch.
- Episodes in the current discovery batch are excluded from the delayed-publishing second pass.
- Enrichment skips episodes still inside the delayed-publishing window (not yet due on YouTube).

### 5.5 Indexing orchestration (`BusinessRules/Indexing/`)

- Full indexing discovers episodes, merges, enriches, filters, then persists.
- Enrich-only indexing does not discover new catalogue episodes; it only enriches stored episodes missing platform links.
- Episodes below minimum duration are marked ignored.
- When `SkipShortEpisodes` is set, short discovered episodes are removed before merge.
- Persist order: enriched episodes, then filtered (removed), then merged existing, then added.
- `LastIndexed` is updated only when indexing succeeds (no merge failures, no Spotify/YouTube bypass, no scheduled YouTube discovery bypass).
- `LatestReleased` on the podcast reflects the most recent release among added and merged episodes.
- Expensive-query flags discovered during indexing are persisted on the podcast.

### 5.6 UrlSubmission (`BusinessRules/UrlSubmission/`)

- Submitting a URL for an episode that already exists enriches missing platform links; it does not create a duplicate.
- When an episode already exists and no fields change, neither podcast nor episode is saved.
- When a new episode is created on an existing podcast, only the episode is saved (unless podcast show metadata was also enriched).
- When podcast show metadata is enriched, the podcast is saved even if the episode is unchanged.
- When `PersistToDatabase` is false, no repository writes occur.
- New podcast submission saves both podcast and episode.

---

## 6. Delivery sequence

### Step 1 — Test infrastructure (PR 1) ✅

- [x] `InMemory*Repository`, fixtures, `EpisodeExpectation`, `SaveCallRecorder`
- [x] Empty `BusinessRules/` folder structure
- [x] One smoke test + two initial platform-identity rule tests (3 passing)

### Step 2 — Domain types stub (PR 2) ✅

- [x] Minimal types in `RedditPodcastPoster.Episodes` (no production wiring)
- [x] Enables Layer 2 tests to compile against destination types

### Step 3 — Domain business rules (PRs 3–5) 🔄 in progress

**Coverage so far:** 8 of ~45 catalog rules (§5.1 platform identity partial + §5.2 release dates partial)

- [x] `PlatformIdentityMatchingRules` — 5 rules (Spotify URL/ID, YouTube ID, negative cases)
- [x] `ReleaseDateMergingRules` — 3 rules (YouTube time backfill, Spotify no backfill, YouTube authority preserve)
- [ ] `CrossPlatformMatchingRules`
- [ ] Remaining `ReleaseDateMergingRules` + `EpisodeMergingRules`
- [ ] Implement **real** `EpisodePlatformMatcher`, `Merger`, `Applier` to make tests pass (this is TDD for the domain layer)
- [ ] Wire `EpisodeMerger` to domain services (behavior must match pre-wiring)

### Step 4 — Orchestration business rules (PRs 6–7)

- `IndexingPersistenceRules`, `IndexingEnrichmentRules`, `IndexingScopeRules`
- `UrlSubmissionPersistenceRules`, `UrlSubmissionEnrichmentRules`

### Step 5 — Adapter rules (PR 8)

- Spotify / Apple / YouTube / ResolvedItem adapters

### Step 6 — Coverage baseline + CI gate (PR 9)

### Step 7 — Refactor phases B–F (implementation)

One phase per PR; all business-rule tests green; no assertion changes.

---

## 7. File layout (target)

```
Class-Libraries/
  RedditPodcastPoster.Episodes/           ← NEW: domain types + services
    Domain/
    Matching/
    Merging/
    Applying/
    Adapters/
  RedditPodcastPoster.Episodes.Tests/     ← NEW: business rules + domain tests
    BusinessRules/
      Matching/
      Merging/
      Enrichment/
      Indexing/
      UrlSubmission/
    Fixtures/
    Fakes/
  RedditPodcastPoster.PodcastServices.Tests/  ← orchestration tests (optional split)
  RedditPodcastPoster.UrlSubmission.Tests/    ← extend with UrlSubmission rules
```

---

## 8. Agent / contributor checklist

Before opening a refactor PR:

- [ ] All applicable business rules have tests with plain-English `DisplayName`
- [ ] Given/When/Then structure in every new test
- [ ] Then-clause uses `EpisodeExpectation` or repository save assertions
- [ ] No existing rule test assertions changed (unless behavior change is explicit)
- [ ] Domain branch coverage meets baseline
- [ ] Incident-pin tests still pass (or superseded by equivalent rule with stricter Then)
- [ ] No new processing logic outside domain services / adapters / applier

---

## 9. References

- Incident context: PRs #866–870 (indexing merge/enrichment fixes)
- Existing starting fixtures: `Persistence.Tests/EpisodeMergerTests.cs`, `C2CAbuserEpisodeMergeTests.cs`, `EpisodeMergerNegativeDelayTests.cs`
- Orchestration entry points: `PodcastUpdater.cs`, `CategorisedItemProcessor.cs`, `EpisodeProvider.cs`
- Release tolerance (migrate, don’t rewrite): `EpisodeReleaseMatchTolerance.cs`

---

## 10. SOLID & specialization architecture

Agent guardrails for where logic lives and how it is wired. The episode pipeline is **one orchestrated flow** with **many small, composable specializations** — not one platform service per concern.

### 10.1 Single Responsibility (SRP)

Each class has **one reason to change**:

| Component | Single responsibility | Changes when… |
|-----------|----------------------|---------------|
| **Adapters** (`IEpisodeCatalogueAdapter`) | Map foreign API / `Resolved*Item` → `EpisodeCandidate` | Platform API shape or mapping quirks change |
| **`EpisodePlatformMatcher`** | Identity + title/duration scoring; delegate release to strategies | Match scoring rules change (not release tolerance) |
| **`IReleaseMatchStrategy`** | Answer “do these releases match for merge?” | Match-time release tolerance rules change |
| **`IReleaseMergePolicy`** | Answer “should incoming release overwrite stored?” | Merge-time backfill / authority rules change |
| **`EpisodePlatformMerger`** | Fill-missing field merge using policies | Generic merge shape changes (not platform release rules) |
| **`EpisodePlatformApplier`** | Apply `EpisodePlatformPatch` → flat `Episode` fields | Persisted field layout changes |
| **Platform enrichers** (`IPlatformEpisodeEnricher`) | Resolve patch for find/enrich (no in-place mutation) | Platform lookup / patch shape changes |
| **Orchestrators** (`PodcastUpdater`, `CategorisedItemProcessor`) | Routing, save order, flags (`SkipEnrichingFromYouTube`) | Workflow / persistence order changes |

**Do not** put match tolerance, merge backfill, adapter mapping, or applier field writes in the same class.

### 10.2 Open/Closed (OCP)

The core pipeline is **closed for modification, open for extension**:

- Register new **`IReleaseMatchStrategy`**, **`IReleaseMergePolicy`**, **`IEpisodeCatalogueAdapter`**, or **`IPlatformEpisodeEnricher`** implementations at the composition root.
- **`EpisodePlatformMatcher`**, **`EpisodePlatformMerger`**, and **`EpisodePlatformApplier`** iterate registered abstractions — they do not grow `switch (service)` branches when a platform gains a new rule.

Adding YouTube negative-delay guard behavior = new strategy class + DI registration, not edits to matcher internals.

### 10.3 Liskov Substitution (LSP)

**`IReleaseMatchStrategy`** implementations must be interchangeable:

- Return **`null`** (or equivalent “not applicable”) when the strategy does not apply to the candidate pair — do not throw or return a sentinel that breaks the chain.
- The matcher walks strategies in registration order; the first non-null result wins.
- Any strategy may be omitted from registration without breaking the pipeline for platforms that do not need it.

Same contract for **`IReleaseMergePolicy`**: return “no opinion” when the policy does not apply; merger applies the first applicable policy or default fill-missing behavior.

### 10.4 Interface Segregation (ISP)

Split interfaces — **no fat `IPlatformEpisodeService`**:

| Interface | Responsibility |
|-----------|----------------|
| **`IEpisodeCatalogueAdapter`** | Map platform payload → `EpisodeCandidate` |
| **`IReleaseMatchStrategy`** | Match-time release comparison |
| **`IReleaseMergePolicy`** | Merge-time release overwrite / backfill |
| **`IPlatformEpisodeEnricher`** | Find patch only (`EpisodePlatformPatch?`); no direct `Episode` mutation |
| **`IEpisodePlatformApplier`** | Apply patch to `Episode` |

Orchestrators depend only on the interfaces they need. A Spotify enricher does not implement Apple merge policy; a YouTube adapter does not implement matching.

Optional side effects (e.g. Spotify expensive-query flag) use a narrow **`IEnrichmentSideEffect`** — not bundled into enricher or applier.

### 10.5 Dependency Inversion (DIP)

- **Orchestration** (`PodcastUpdater`, enricher template, UrlSubmission processor) depends on **`IEpisodePlatformMatcher`**, **`IEpisodePlatformMerger`**, **`IEpisodePlatformApplier`**, and enricher abstractions — not on Spotify/Apple/YouTube concrete types.
- **Platform projects** implement adapters, strategies, policies, and enrichers.
- **Composition root** (e.g. `services.AddEpisodesDomain()`) wires implementations; domain library has no reference to platform assemblies.

### 10.6 Where specializations live

| Concern | Location | Examples |
|---------|----------|----------|
| **Mapping** | Adapters | Spotify → `ReleasePrecision.DateOnly`; Apple → full datetime; YouTube → as-is from API |
| **Match-time release** | `IReleaseMatchStrategy` chain (order matters) | `ExactReleaseMatchStrategy`, `SpotifyCatalogueReleaseMatchStrategy`, `YouTubePublishDelayMatchStrategy`, `YouTubeNegativeDelayGuardStrategy` |
| **Merge-time release** | `IReleaseMergePolicy` (separate from matching) | `YouTubeTimeBackfillMergePolicy`, `SpotifyNoTimeBackfillMergePolicy`, `YouTubeAuthoritativePreserveMergePolicy`, `AppleTimeBackfillMergePolicy` |
| **Enrichment** | Platform enrichers + optional side effects | `IPlatformEpisodeEnricher` returns patch; `IEnrichmentSideEffect` for Spotify expensive-query persistence |
| **Discovery routing** | Existing retrieval handlers | Unchanged; handlers produce candidates via adapters |

**Matching ≠ merging:** tolerance during “are these the same episode?” lives in strategies; “should we overwrite midnight UTC with YouTube time?” lives in merge policies. Never conflate the two in one class.

### 10.7 DI composition example

Registration order for **`IReleaseMatchStrategy`** matters — first applicable strategy wins:

```csharp
services.AddEpisodesDomain(options =>
{
    options.RegisterMatchStrategies(services =>
    {
        services.AddSingleton<IReleaseMatchStrategy, ExactReleaseMatchStrategy>();
        services.AddSingleton<IReleaseMatchStrategy, SpotifyCatalogueReleaseMatchStrategy>();
        services.AddSingleton<IReleaseMatchStrategy, YouTubePublishDelayMatchStrategy>();
        services.AddSingleton<IReleaseMatchStrategy, YouTubeNegativeDelayGuardStrategy>();
    });

    options.RegisterMergePolicies(services =>
    {
        services.AddSingleton<IReleaseMergePolicy, YouTubeAuthoritativePreserveMergePolicy>();
        services.AddSingleton<IReleaseMergePolicy, YouTubeTimeBackfillMergePolicy>();
        services.AddSingleton<IReleaseMergePolicy, SpotifyNoTimeBackfillMergePolicy>();
        services.AddSingleton<IReleaseMergePolicy, AppleTimeBackfillMergePolicy>();
    });

    options.RegisterAdapters(/* per platform at composition root */);
    options.RegisterEnrichers(/* Spotify, Apple, YouTube */);
});
```

Core services (`EpisodePlatformMatcher`, `EpisodePlatformMerger`, `EpisodePlatformApplier`) are registered once inside `AddEpisodesDomain`; platform assemblies only add strategy/policy/adapter/enricher implementations.

### 10.8 Anti-patterns (explicit)

| Anti-pattern | Why it violates SOLID | Do instead |
|--------------|----------------------|------------|
| `switch (service)` inside matcher, merger, or applier | OCP — core pipeline modified per platform | Register strategy/policy/adapter via DI |
| Static god-class **`EpisodeReleaseMatchTolerance`** called from enrichers, merger, and matcher | SRP + DIP — one change ripples everywhere | Extract methods into strategies/policies; keep semantics (see §10.9) |
| Enricher mutating `Episode` in three code paths (indexing, UrlSubmission, discovery) | SRP + ISP — enricher owns persistence shape | Return `EpisodePlatformPatch`; applier writes once |
| Merge policy embedded in match strategy | SRP — match and merge change for different reasons | Separate `IReleaseMatchStrategy` and `IReleaseMergePolicy` |
| Fat `IPlatformEpisodeService` (find + match + merge + apply) | ISP — callers depend on unused methods | Split interfaces per §10.4 |
| Orchestrator calling `EpisodeReleaseMatchTolerance` directly | DIP — orchestration tied to static detail | Inject matcher/merger; orchestration only coordinates |
| Adapter performing merge or match | SRP — mapping and processing coupled | Adapter stops at `EpisodeCandidate` |
| Changing strategy order without documenting rule interaction | LSP / behavior — first-win semantics change | Document order in DI registration; cover with business-rule tests |

### 10.9 Migrating `EpisodeReleaseMatchTolerance`

Existing **`EpisodeReleaseMatchTolerance`** (`PodcastServices.Abstractions`) is the **source of truth for current semantics**. During Phase A–D:

1. **Characterize** each public method with business-rule tests (many already exist in `AppleEpisodeReleaseMatchToleranceTests` and incident-pin tests).
2. **Move** logic into named strategy/policy classes **without rewriting** — copy behavior, then delete the static call sites.
3. **Map** roughly as follows (exact class names may differ; semantics must not):

| Today (`EpisodeReleaseMatchTolerance`) | Destination |
|----------------------------------------|-------------|
| `EpisodesReleaseMatch`, `GetToleranceTicks`, delay-adjusted comparisons | `IReleaseMatchStrategy` implementations |
| `SpotifyCatalogueReleaseMatches` | `SpotifyCatalogueReleaseMatchStrategy` |
| YouTube delay / YouTube-first Spotify catalogue paths | `YouTubePublishDelayMatchStrategy`, `YouTubeNegativeDelayGuardStrategy` |
| `ShouldPreserveYouTubeAuthoritativeRelease` | `YouTubeAuthoritativePreserveMergePolicy` |
| Midnight UTC time backfill (YouTube / Apple) | `YouTubeTimeBackfillMergePolicy`, `AppleTimeBackfillMergePolicy` |
| Spotify date-only (no time overwrite) | `SpotifyNoTimeBackfillMergePolicy` |
| `GetAudioReleaseForPlatformLookup` | Adapter or enricher request factory (platform lookup input), not merger |

**Do not** “simplify” tolerance while extracting. Suspected bugs stay characterized as current behavior (§4.7); fixes are separate PRs after strategies own the logic.
