# RedditPodcastPoster.Episodes — architecture

Platform-agnostic episode domain for **match**, **merge**, **apply**, and **adapt** operations. Platform API types stay in `PodcastServices.{Spotify,Apple,YouTube}`; this library owns the normalized model and algorithms.

**Related docs:** [Step 7 checklist](../../plans/episode-domain-refactor/STEP-7-CHECKLIST.md) · [Episode domain refactor plan](../../plans/episode-domain-refactor/README.md)

---

## Design principles

| Principle | Detail |
|-----------|--------|
| **No platform references** | `RedditPodcastPoster.Episodes` references only `Models` and `Text`. Spotify/Apple/YouTube assemblies reference Episodes, not the reverse. |
| **Adapters map** | Foreign API / resolved-item DTOs → `EpisodeCandidate` at boundaries. |
| **Strategies match** | Release tolerance and cross-platform delay logic live in `IReleaseMatchStrategy` implementations. |
| **Policies merge** | Release backfill and authority rules live in `IReleaseMergePolicy` implementations. |
| **Applier writes** | All platform field writes on an existing `Episode` go through `IEpisodePlatformApplier` (or `IPlatformEnrichmentApplicator` for indexing enrich). |
| **Orchestrators coordinate** | `PodcastUpdater`, `PodcastServicesEpisodeEnricher`, and UrlSubmission `EpisodeEnricher` call domain services; they do not embed match/merge/apply logic. |

---

## Folder layout

```
RedditPodcastPoster.Episodes/
├── Domain/           EpisodeCandidate, PlatformLink, ReleaseInfo, EpisodePlatformPatch
├── Adapters/         IEpisodeCatalogueAdapter<T>, catalogue + resolved-item adapters
├── Applying/         IEpisodePlatformApplier, IPlatformEnrichmentApplicator
├── Matching/         IEpisodePlatformMatcher + IReleaseMatchStrategy chain
├── Merging/          IEpisodePlatformMerger + IReleaseMergePolicy chain
├── Factories/        IEpisodeFromCandidateFactory (candidate → new Episode)
├── Extensions/       Identity/mapping helpers; ServiceCollectionExtensions (AddEpisodesDomain)
```

---

## Diagram 1 — Episodes domain (internal)

How types inside this assembly relate. Solid arrows are “uses” or “produces”; dashed arrows are strategy/policy chains.

```mermaid
flowchart TB
    subgraph Models["RedditPodcastPoster.Models"]
        EP[(Episode / Podcast)]
    end

    subgraph DomainTypes["Domain value types"]
        EC[EpisodeCandidate]
        PL[PlatformLink]
        RI[ReleaseInfo]
        PATCH[EpisodePlatformPatch]
        EC --- PL & RI
    end

    subgraph Adapters["Adapters"]
        CAT[IEpisodeCatalogueAdapter&lt;T&gt;<br/>Spotify / Apple / YouTube]
        RES[Resolved*ItemAdapter<br/>UrlSubmission path]
        FACT[IEpisodeFromCandidateFactory]
        CAT --> EC
        RES --> EC
        FACT --> EP
    end

    subgraph Matching["Matching"]
        MATCH[IEpisodePlatformMatcher<br/>EpisodePlatformMatcher]
        RS1[ExactReleaseMatchStrategy]
        RS2[SpotifyCatalogueReleaseMatchStrategy]
        RS3[YouTubePublishDelayMatchStrategy]
        MATCH -.-> RS1 & RS2 & RS3
    end

    subgraph Merging["Merging"]
        MERGE[IEpisodePlatformMerger<br/>EpisodePlatformMerger]
        MP1[YouTubeAuthoritativePreserve]
        MP2[YouTubeTimeBackfill]
        MP3[SpotifyNoTimeBackfill]
        MP4[AppleTimeBackfill]
        MERGE -.-> MP1 & MP2 & MP3 & MP4
    end

    subgraph Applying["Applying"]
        APPLIER[IEpisodePlatformApplier<br/>EpisodePlatformApplier]
        ENRICH[IPlatformEnrichmentApplicator<br/>PlatformEnrichmentApplicator]
        ENRICH --> APPLIER
        ENRICH -.-> MP2 & MP4
    end

    CAT --> FACT
    MATCH --> EP
    MERGE --> APPLIER
    MERGE --> EP
    APPLIER --> PATCH
    APPLIER --> EP
    ENRICH --> EP
```

### Responsibilities

| Component | Role |
|-----------|------|
| **EpisodeCandidate** | Normalized platform snapshot (title, duration, links, release) before apply or factory create. |
| **EpisodePlatformMatcher** | Identity match, title/duration heuristics, catalogue lookup (`FindCatalogueMatchByLength/ByDate`, `IsCatalogueMatch`). Delegates release decisions to strategies (first non-null `bool?` wins). |
| **EpisodePlatformMerger** | Merges incoming candidate into stored episode in place; uses applier for fill-missing platform fields and policy chain for release. |
| **EpisodePlatformApplier** | Writes `EpisodePlatformPatch` onto `Episode` (links, description, release) without overwriting existing values unless policy allows. |
| **PlatformEnrichmentApplicator** | Indexing enrich entry point: candidate → patch → applier + release backfill policies. Returns `PlatformEnrichmentResult`. |
| **Catalogue adapters** | Map platform catalogue inputs (`SpotifyCatalogueInput`, etc.) to `EpisodeCandidate`. |
| **Resolved-item adapters** | Map UrlSubmission `Resolved*Item` DTOs to `EpisodeCandidate`. |

### Strategy and policy order

Registered in `AddEpisodesDomain()` (`Extensions/ServiceCollectionExtensions.cs`, namespace `RedditPodcastPoster.Episodes.Extensions`):

**Match strategies** (chain — first applicable non-null result):

1. `ExactReleaseMatchStrategy`
2. `SpotifyCatalogueReleaseMatchStrategy`
3. `YouTubePublishDelayMatchStrategy`

**Merge policies** (chain — first decisive opinion):

1. `YouTubeAuthoritativePreserveMergePolicy`
2. `YouTubeTimeBackfillMergePolicy`
3. `SpotifyNoTimeBackfillMergePolicy`
4. `AppleTimeBackfillMergePolicy`

---

## Diagram 2 — Episodes + PodcastServices + platform specializations

How the domain sits between orchestration and platform assemblies. **Episodes is shared**; each platform project plugs in providers, enrichers, resolvers, and finders.

```mermaid
flowchart TB
    subgraph Host["Composition root (Indexer / Api / CLI)"]
        DI["AddEpisodesDomain()<br/>AddSpotifyServices() · AddAppleServices() · AddYouTubeServices()<br/>AddPodcastServices() · AddUrlSubmission()"]
    end

    subgraph Domain["RedditPodcastPoster.Episodes"]
        direction TB
        M[Matcher]
        MG[Merger]
        EA[EnrichmentApplicator]
        AP[Applier]
        AD[Adapters + Factory]
    end

    subgraph Abstractions["PodcastServices.Abstractions"]
        TPL[PlatformEpisodeEnricherTemplate]
        CTX[EnrichmentRequest / EnrichmentContext]
    end

    subgraph Orchestration["RedditPodcastPoster.PodcastServices"]
        PU[PodcastUpdater]
        PSE[PodcastServicesEpisodeEnricher]
        EP[EpisodeProvider<br/>Common]
        EM[EpisodeMerger facade<br/>Persistence]
    end

    subgraph Spotify["PodcastServices.Spotify"]
        SP_P[SpotifyEpisodeProvider]
        SP_E[SpotifyEpisodeEnricher]
        SP_R[SpotifyEpisodeResolver]
        SP_F[SearchResultFinder]
        SP_S[SpotifyExpensiveQuerySideEffect]
    end

    subgraph Apple["PodcastServices.Apple"]
        AP_P[AppleEpisodeProvider]
        AP_E[AppleEpisodeEnricher]
        AP_R[AppleEpisodeResolver]
    end

    subgraph YouTube["PodcastServices.YouTube"]
        YT_P[YouTubeEpisodeProvider]
        YT_E[YouTubeEpisodeEnricher]
        YT_R[YouTubeItemResolver]
        YT_F[SearchResultFinder · PlaylistItemFinder]
    end

    subgraph UrlSubmission["RedditPodcastPoster.UrlSubmission"]
        UE[EpisodeEnricher]
        PP[PodcastProcessor]
    end

    DI --> Domain & Orchestration & Spotify & Apple & YouTube & UrlSubmission

    PU --> EP --> SP_P & AP_P & YT_P
    PU --> EM --> M & MG
    PU --> PSE --> SP_E & AP_E & YT_E

    SP_P & AP_P & YT_P --> AD
    SP_E & AP_E & YT_E --> TPL --> EA
    SP_E --> SP_R & SP_F & SP_S
    AP_E --> AP_R
    YT_E --> YT_R & YT_F & AP

    SP_F & YT_F --> M
    SP_R & AP_R & YT_R --> AD

    PP --> UE --> AD
    UE --> AP

    EA --> AP
    MG --> AP
```

### Platform specialization pattern

Each platform assembly follows the same shape:

| Layer | Spotify | Apple | YouTube |
|-------|---------|-------|---------|
| **Discovery** | `SpotifyEpisodeProvider` | `AppleEpisodeProvider` | `YouTubeEpisodeProvider` |
| **Enrich** | `SpotifyEpisodeEnricher` | `AppleEpisodeEnricher` | `YouTubeEpisodeEnricher` |
| **Resolve** | `SpotifyEpisodeResolver` | `AppleEpisodeResolver` | `YouTubeItemResolver` |
| **Find / match helper** | `SearchResultFinder` | (resolver uses matcher) | `SearchResultFinder`, `PlaylistItemFinder` |
| **Side effects** | `SpotifyExpensiveQuerySideEffect` | — | — |
| **Catalogue adapter** | `SpotifyEpisodeAdapter` | `AppleEpisodeAdapter` | `YouTubeEpisodeAdapter` |

Platform enrichers inherit `PlatformEpisodeEnricherTemplate` (in Abstractions):

```
Resolver finds catalogue item
  → IEpisodeCatalogueAdapter.Adapt() → EpisodeCandidate
  → [optional] IEpisodePlatformMatcher.CatalogueReleaseMatches filter
  → PlatformEpisodeEnricherTemplate.ApplyResolvedCandidate()
       → IPlatformEnrichmentApplicator.Apply()
       → PlatformEnrichmentResult.ApplyTo(EnrichmentContext)
```

YouTube enricher additionally calls `IEpisodePlatformApplier` directly for link-only backfill and supplemental video metadata (description, thumbnail).

---

## Diagram 3 — Runtime paths

Two production paths consume Episodes differently.

```mermaid
flowchart LR
    subgraph Indexing["Indexing path (Indexer)"]
        direction TB
        I1[PodcastUpdater.Update]
        I2[EpisodeProvider → platform Provider]
        I3[EpisodeMerger → Matcher + Merger]
        I4[PodcastServicesEpisodeEnricher]
        I5[Platform enricher → EnrichmentApplicator]
        I6[(Cosmos save)]
        I1 --> I2 --> I3 --> I4 --> I5 --> I6
    end

    subgraph Submit["UrlSubmission path (Api)"]
        direction TB
        S1[UrlCategoriser → CategorisedItem]
        S2[PodcastProcessor]
        S3[EpisodeEnricher]
        S4[Resolved*ItemAdapter → Applier]
        S5[(Cosmos save)]
        S1 --> S2 --> S3 --> S4 --> S5
    end

    Domain2[RedditPodcastPoster.Episodes]
    Indexing --> Domain2
    Submit --> Domain2
```

| Path | Host | Domain entry points | Platform enrichers? |
|------|------|---------------------|---------------------|
| **Indexing** | Indexer | Matcher, Merger, EnrichmentApplicator, catalogue adapters | Yes — Spotify → Apple → YouTube per episode |
| **UrlSubmission** | Api | Applier, resolved-item adapters | No — resolved URL already known |

**Indexing enrich order** (`PodcastServicesEpisodeEnricher`): for each new episode, Spotify then Apple then YouTube when links/IDs are missing. Delayed YouTube publishing triggers a **second pass** on recently expired episodes (orchestrator concern, not in platform enrichers).

---

## Dependency graph (projects)

Current state (episode-domain slice). **Red edges** are layering debt — see [Phase F F13–F20](../../plans/episode-domain-refactor/STEP-7-CHECKLIST.md#project-dependency-red-flags-misplaced-types).

```mermaid
flowchart BT
    Models[RedditPodcastPoster.Models]
    Episodes[RedditPodcastPoster.Episodes]
    PersAbstr[Persistence.Abstractions]
    Abstr[PodcastServices.Abstractions]
    PS[PodcastServices]
    SP[PodcastServices.Spotify]
    AP[PodcastServices.Apple]
    YT[PodcastServices.YouTube]
    Pers[Persistence]
    US[UrlSubmission]
    Text[Text]

    Episodes --> Models
    Abstr --> Episodes
    Abstr --> Models
    Abstr --> PersAbstr
    SP & AP & YT --> Episodes
    SP & AP & YT --> Abstr
    PS --> SP & AP & YT & Abstr
    Pers --> Episodes
    Pers -.->|orphan?| Abstr
    Pers --> PersAbstr
    US --> Episodes
    US --> PS
    US --> AP & SP
    YT --> PersAbstr
    Text --> PersAbstr
```

### Dependency red flags (Phase F)

| Issue | Why it matters | Phase F action |
|-------|----------------|----------------|
| `Persistence → Episodes` | Storage layer depends on domain algorithms; merge orchestration is not persistence | **F13** — move `EpisodeMatcher`/`EpisodeMerger` to `PodcastServices` |
| `IEpisodeMatcher` / `EpisodeMergeResult` in `Persistence.Abstractions` | Orchestration contracts mislabeled as persistence; forces `IndexPodcastResult → PersAbstr` | **F14**, **F16** |
| `Persistence → PodcastServices.Abstractions` (csproj only) | Unused reference — likely stale | **F15** |
| `UrlSubmission → PodcastServices` (concrete) + platform `Resolved*Item` on `CategorisedItem` | Submit path pulls full indexing aggregator + foreign platform models | **F17** |
| `Text` hosts `KnownTermsRepository` | Text library implements Cosmos repos | **F18** |
| `Episodes.TestSupport → Persistence` | Domain test helpers construct persistence facades | **F19** (after F13) |
| `PodcastServices.YouTube → Persistence.Abstractions` | Platform assembly knows repo interfaces for quota state | **F20** |

### Target graph (after Phase F layering)

```mermaid
flowchart BT
    Models[Models]
    Episodes[Episodes]
    PersAbstr[Persistence.Abstractions]
    Abstr[PodcastServices.Abstractions]
    PS[PodcastServices]
    SP & AP & YT[Platform assemblies]
    Pers[Persistence]
    US[UrlSubmission]

    Episodes --> Models
    Abstr --> Episodes & Models
    PS --> Episodes & Abstr & PersAbstr
    SP & AP & YT --> Episodes & Abstr
    Pers --> PersAbstr & Models
    US --> Episodes & Abstr
```

`PodcastServices` owns merge orchestration and references `IEpisodeRepository` via abstractions only. `Persistence` has no `Episodes` reference.

---

## DI registration

`AddEpisodesDomain()` must be called **explicitly** at each host composition root that needs matcher, merger, or applier — it is not nested inside `AddRepositories()` or `AddUrlSubmission()`. Import `RedditPodcastPoster.Episodes.Extensions` (same pattern as `Persistence.Extensions`).

| Extension | Registers |
|-----------|-----------|
| `AddEpisodesDomain()` | Applier, enrichment applicator, merger, matcher, 3 strategies, 4 policies, factory, 3 catalogue adapters |
| `AddSpotifyServices()` | Spotify provider, enricher, resolver, finder, side effect |
| `AddAppleServices()` | Apple provider, enricher, resolver |
| `AddYouTubeServices()` | YouTube provider, enricher, resolver, finders |
| `AddPodcastServices()` | `PodcastUpdater`, `PodcastServicesEpisodeEnricher`, metadata handlers (does **not** register Episodes domain) |
| `AddUrlSubmission()` | UrlSubmission pipeline (does **not** register Episodes domain) |

**Typical Indexer host:** `AddEpisodesDomain()` → `AddRepositories()` → `Add*Services()` → `AddPodcastServices()`.

**Typical Api host:** same, plus `AddUrlSubmission()`.

---

## Key source files

| Area | Path |
|------|------|
| DI | `Extensions/ServiceCollectionExtensions.cs` |
| Matcher | `Matching/IEpisodePlatformMatcher.cs`, `Matching/EpisodePlatformMatcher.cs` |
| Merger | `Merging/IEpisodePlatformMerger.cs`, `Merging/EpisodePlatformMerger.cs` |
| Applier | `Applying/IEpisodePlatformApplier.cs`, `Applying/EpisodePlatformApplier.cs` |
| Enrichment applicator | `Applying/IPlatformEnrichmentApplicator.cs`, `Applying/PlatformEnrichmentApplicator.cs` |
| Enricher template | `../RedditPodcastPoster.PodcastServices.Abstractions/Enriching/PlatformEpisodeEnricherTemplate.cs` |
| Indexing orchestrator | `../RedditPodcastPoster.PodcastServices/PodcastUpdater.cs` |
| Enrich facade | `../RedditPodcastPoster.PodcastServices/PodcastServicesEpisodeEnricher.cs` |
| Persistence facades | `../RedditPodcastPoster.Persistence/EpisodeMatcher.cs`, `EpisodeMerger.cs` |
| UrlSubmission enrich | `../RedditPodcastPoster.UrlSubmission/EpisodeEnricher.cs` |
