# Implementation Checklist Mapped to This Repository

> Naming note: this checklist uses target-model terminology in prose. Explicit type/interface names are listed only where implementation mapping is required.

## Current Status Snapshot (code-based)

### Done
- `Console-Apps/LegacyPodcastToV2Migration/*` exists and migrates legacy data into split target containers.
- Legacy-to-target migration now uses legacy repositories as sources for:
  - podcasts/episodes
  - lookups (`KnownTerms`, `EliminationTerms`)
  - push subscriptions
  - subjects
  - discovery (all documents)
- Target repositories for split model are implemented and wired:
  - target podcast repository (`IPodcastRepositoryV2` / `PodcastRepositoryV2`)
  - target episode repository (`IEpisodeRepository` / `EpisodeRepository`)
  - target subject repository (`ISubjectRepositoryV2`)
  - target discovery repository (`IDiscoveryResultsRepositoryV2`)
  - target lookup repository (`ILookupRepositoryV2`)
  - target push-subscription repository (`IPushSubscriptionRepositoryV2`)
- Migration mapping carries expanded podcast metadata into the target podcast model and maps episode records into discrete target `Episode` entities.
- Sampled parity verification tooling is implemented for:
  - podcasts
  - subjects
  - discovery (deep checks including arrays and url objects)
  - lookups
  - push subscriptions
  - episodes
- Search datasource query migration in `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs` is implemented:
  - datasource now targets `Episodes` container
  - query shape switched from `JOIN e IN p.episodes` to `FROM episodes e`
  - high-watermark semantics now use `e._ts`
- Episode projection now carries `podcastRemoved` and search filtering uses it in datasource query.
- `Cloud/Api/Handlers/PodcastHandler.cs` now uses the target podcast repository with detached episode hydration via `IEpisodeRepository` for podcast metadata fan-out.
- Runtime/API de-embedding progress includes:
  - `Cloud/Api/Handlers/EpisodeHandler.cs` migrated `Get` endpoint to detached episode + podcast repositories.
  - `Cloud/Api/Handlers/PublicHandler.cs` migrated to detached episode + podcast repositories.
  - `Cloud/Api/Services/DiscoveryResultsService.cs` migrated to target podcast + detached episode repositories for visible episode counts.
- Console de-embedding progress includes:
  - `Console-Apps/RemoveEpisodes/Processor.cs`
  - `Console-Apps/UnremoveEpisodes/Processor.cs`
  - `Console-Apps/EnrichPodcastWithImages/Processor.cs`
  - `Console-Apps/FixDatesFromApple/Processor.cs`
  - `Console-Apps/Tweet/TweetProcessor.cs`
  - `Console-Apps/KVWriter/KVWriterProcessor.cs`
  - `Console-Apps/TextClassifierTraining/TrainingDataProcessor.cs`
  - `Console-Apps/IndexAllEpisodesAudit/IndexAllEpisodesAuditProcessor.cs`
  - `Console-Apps/EliminateExistingEpisodes/Procesor.cs`
  - `Console-Apps/Poster/PostProcessor.cs`
  - `Console-Apps/EnrichExistingEpisodesFromPodcastServices/EnrichPodcastEpisodesProcessor.cs`

## Phase 1: New Persistence Contracts
- [ ] Remove episode-embedded query methods from podcast abstraction.
- [ ] Keep podcast-only CRUD/query operations.
- [x] Add episode-centric operations, for example:
  - [x] `GetEpisode(...)`
  - [x] `GetByPodcastId(...)`
  - [x] `Save(...)`
  - [x] `Delete(...)`
  - [x] query methods replacing legacy `x.Episodes.Any(...)` patterns.
- [x] Remove `Episodes` member.
- [x] Add `PodcastId` member.
- [x] Ensure JSON metadata and persistence attributes are aligned with Cosmos model.
- [x] Add/search-support denormalized fields needed by indexer query (`podcastName`, `podcastSearchTerms`, language strategy).
- [ ] Do not add duplicate compact identifier members for IDs already present (`spotifyId`, `youTubeId`, `appleId`).
- [x] Add metadata sync marker for drift detection (for example `podcastMetadataVersion` or `podcastMetadataUpdatedAt`).
- [ ] Introduce reduced-key schema (`CompactSearchRecord`) for search index payload minimization.
- [ ] Keep schema version marker (for example `sv`) for UI compatibility.
- [ ] Remove full URL fields from indexed record.
- [ ] Map compact keys from existing episode IDs (`sid`/`yid`/`aid`) and derive Apple slug key (`as`) from Apple URL regex.

## Phase 2: Cosmos Repositories and Container Wiring
- [ ] Remove embedded-episode mutation logic.
- [x] Keep podcast metadata operations only.
- [x] Implement `IEpisodeRepository` against `Episodes`.
- [x] Use partition key `/podcastId` for point and scoped queries.
- [ ] Align local/file persistence model to separate podcast and episode entities (if required for local tooling/tests).
- [x] Register `IEpisodeRepository`.
- [ ] Add feature flag support for model cutover.

## Phase 3: API/Core Refactor
- [ ] Replace podcast-embedded episode lookup and mutation.
- [x] Use `IEpisodeRepository` for episode lifecycle operations.
- [x] Use target podcast repository only for podcast metadata retrieval.
- [x] Replace `podcast.Episodes` usage for counts, selection, and indexing lists.
- [x] Query episodes through `IEpisodeRepository` by `podcastId`.
- [x] Trigger episode metadata fan-out updates when podcast properties affecting search are changed.
- [x] Source episodes via `IEpisodeRepository`.
- [ ] Ensure index writes map to reduced-key `CompactSearchRecord` contract.
- [ ] Refactor flows that assume embedded episodes.
- [ ] Migrate episode processing helpers to repository-driven access.
- [x] Replace datasource query in `CreateDataSource` from embedded-episode join to `Episodes`-container query.
- [x] Switch high-watermark semantics from `p._ts` to `e._ts`.
- [x] Validate query field mapping still matches reduced-key `EpisodeSearchRecord` schema.
- [ ] Remaining runtime paths from static scan:
  - [x] `Class-Libraries/RedditPodcastPoster.Common/Episodes/PodcastEpisodeFilter.cs` → ✅ **V2 variant created**: `IPodcastEpisodeFilterV2` / `PodcastEpisodeFilterV2` (returns `PodcastEpisodeV2`)
  - [x] `Class-Libraries/RedditPodcastPoster.Common/Episodes/PodcastEpisodePoster.cs` → ✅ **V2 variant created**: `IPodcastEpisodePosterV2` / `PodcastEpisodePosterV2` (accepts `PodcastEpisodeV2`)
  - [x] `Class-Libraries/RedditPodcastPoster.Common/Podcasts/PodcastFilter.cs` → ✅ **V2 variant created**: `IPodcastFilterV2` / `PodcastFilterV2`
  - [x] `Class-Libraries/RedditPodcastPoster.Common/PodcastEpisodeProvider.cs` → ✅ **V2 variant created**: `IPodcastEpisodeProviderV2` / `PodcastEpisodeProviderV2` (returns `PodcastEpisodeV2`)
  - [x] `Class-Libraries/RedditPodcastPoster.UrlSubmission/Factories/PodcastAndEpisodeFactory.cs` → ✅ **V2 variant created**: `IPodcastAndEpisodeFactoryV2` / `PodcastAndEpisodeFactoryV2`
  - [x] `Class-Libraries/RedditPodcastPoster.UrlSubmission/PodcastProcessor.cs` → ✅ **V2 variant created**: `IPodcastProcessorV2` / `PodcastProcessorV2`

### V2 Service Migration Strategy ✅ COMPLETE
- ✅ Created V2 variants of ALL core services
- ✅ V2 services use `IPodcastRepositoryV2` and `IEpisodeRepository` for detached episodes
- ✅ All V2 services registered in DI for gradual consumer migration
- ✅ Legacy services remain functional during transition
- ✅ **V2 interfaces work ONLY with V2 models** (`PodcastEpisodeV2`) - clean separation
- ✅ **Conversion explicit at boundaries** using `.ToLegacy()` extension methods
- ✅ Created `PodcastEpisodeV2` model for native V2 model pairing
- ✅ **URL submission V2 services complete** - entire ingestion pipeline now has V2 support
- 📋 See `docs/migration/v2-implementation-index.md` for complete service inventory
- 📋 See `docs/migration/architectural-cleanup-summary.md` for design decisions

## Phase 4: UI and Contract Migration
- [ ] Add support for `CompactSearchRecord` key names.
- [ ] Reconstruct URLs client-side from compact keys mapped from existing IDs (`sid`/`yid`/`aid`).
- [ ] Derive Apple slug (`as`) from Apple URL using regex when needed.
- [ ] Support schema version (`sv`) during transition.
- [ ] Keep backward compatibility with legacy search record keys during rollout window.

## Phase 5: Console/Processor Refactor
Update processors identified from code scan to stop using `podcast.Episodes`:

- [x] `Console-Apps/AddAudioPodcast/AddAudioPodcastProcessor.cs` ✅ **COMPLETED** - migrated to IEpisodeRepository
- [x] `Console-Apps/RemoveEpisodes/Processor.cs`
- [x] `Console-Apps/UnremoveEpisodes/Processor.cs`
- [x] `Console-Apps/EnrichPodcastWithImages/Processor.cs`
- [x] `Console-Apps/EnrichYouTubeOnlyPodcasts/EnrichYouTubePodcastProcessor.cs` ✅ **COMPLETED** - migrated to IEpisodeRepository
- [x] `Console-Apps/FixDatesFromApple/Processor.cs`
- [x] `Console-Apps/Tweet/TweetProcessor.cs`
- [x] `Console-Apps/KVWriter/KVWriterProcessor.cs`
- [x] `Console-Apps/TextClassifierTraining/TrainingDataProcessor.cs`
- [x] `Console-Apps/IndexAllEpisodesAudit/IndexAllEpisodesAuditProcessor.cs`
- [x] `Console-Apps/EliminateExistingEpisodes/Procesor.cs`
- [x] `Console-Apps/EnrichExistingEpisodesFromPodcastServices/EnrichPodcastEpisodesProcessor.cs`
- [x] `Console-Apps/Poster/PostProcessor.cs`
- [ ] audit any new embedded-episode usages introduced after this checklist snapshot.

### Phase 5 Migration Completed! ✅
- ✅ `AddAudioPodcastProcessor` - NOW uses IEpisodeRepository for all episode operations
- ✅ `EnrichYouTubePodcastProcessor` - NOW uses IEpisodeRepository for all episode operations
- ✅ Created `IEpisodeMerger` service to extract merge logic from PodcastRepository
- ✅ `PodcastUpdater` is the active `IPodcastUpdater` implementation using detached episodes (`IPodcastRepositoryV2` + `IEpisodeRepository`)
- ✅ Both processors persist episodes to detached Episodes container
- ✅ Both processors persist podcast metadata to V2 Podcasts container

## Phase 6: Migration Tooling
- [x] Read legacy `CultPodcasts` documents.
- [x] Emit podcasts into `Podcasts`.
- [x] Emit episodes into `Episodes` with `podcastId`.
- [x] Populate denormalized episode metadata fields needed for search.
- [x] Validate existing IDs (`spotifyId`, `youTubeId`, `appleId`) are populated for compact search mapping.
- [x] Write reconciliation outputs (counts and mismatch details).

## Phase 7: Verification and Cutover Readiness
- [ ] Build solution successfully after removing `Podcast.Episodes`.
- [ ] Ensure zero references to embedded-episode patterns in runtime paths.
- [x] Ensure zero `JOIN e IN p.episodes` in search-index datasource definitions.
- [ ] Validate publish/delete/unremove/index/tweet flows.
- [ ] Validate podcast retrieval and rename side effects.
- [ ] Validate search indexing continues to produce expected records after datasource query migration.
- [x] Validate podcast metadata updates fan out to episodes and surface in search results.
- [ ] Validate URL reconstruction in UI from compact keys sourced from existing IDs and Apple URL regex slug derivation.
- [x] Match podcast totals.
- [x] Match episode totals.
- [ ] Match per-podcast episode counts.
- [ ] Match search index document totals and sampled search fields between old and new query model.
- [ ] Validate RU and latency profile on `Episodes`.
- [ ] Validate RU and latency profile of fan-out metadata updates.
- [ ] Validate search-index storage reduction after schema/key minimization.
- [ ] Confirm no writes reach legacy container post-cutover.
- [ ] Perform final adapter review for all `servicePodcast`, `serviceEpisode`, `legacyPodcast`, and `legacyEpisode` instances, and eliminate non-migration-path usages.

## Phase 8: Legacy Decommissioning Targets (Current Priority)
- [x] Migrate `ITweetPoster` signatures and implementations to `PodcastEpisodeV2`.
- [x] Migrate `IBlueskyPoster` signatures and implementations to `PodcastEpisodeV2`.
- [x] Migrate `IShortnerService` signatures and implementations to `PodcastEpisodeV2`.
- [x] Remove runtime `.ToLegacy()` boundaries in:
  - [x] `Console-Apps/Poster/PostProcessor.cs`
  - [x] `Class-Libraries/RedditPodcastPoster.Twitter/Tweeter.cs`
  - [x] `Class-Libraries/RedditPodcastPoster.Bluesky/BlueskyPostManager.cs`
  - [x] `Cloud/Api/Handlers/EpisodeHandler.cs`
- [ ] Remove temporary legacy compatibility overloads once no callers remain:
  - [ ] `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify/Factories/FindSpotifyEpisodeRequestFactory.cs`
  - [ ] `Class-Libraries/RedditPodcastPoster.PodcastServices.Apple/FindAppleEpisodeRequestFactory.cs`
- [ ] Remove obsolete conversion helpers once no callers remain:
  - [ ] `ToLegacyPodcast`
  - [ ] `ToLegacyEpisode`
  - [ ] `PodcastEpisodeV2.ToLegacy()`
- [ ] Decommission legacy service variants after consumer migration:
  - [ ] `IPodcastEpisodeProvider` / `PodcastEpisodeProvider`
  - [ ] `IPodcastEpisodePoster` / `PodcastEpisodePoster`
- [ ] Confirm final rule: legacy model runtime usage remains only in `PodcastRepository` and `LegacyPodcastToV2Migration`.
