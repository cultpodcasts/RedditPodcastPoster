# Implementation Checklist Mapped to This Repository

> Stage notes:
> - [`stages/pr1-modeltype-immutability.md`](./stages/pr1-modeltype-immutability.md)
> - [`stages/pr1-episode-repository-scaffold.md`](./stages/pr1-episode-repository-scaffold.md)
> - [`stages/pr1-episode-repository-podcastid-partition.md`](./stages/pr1-episode-repository-podcastid-partition.md)
> - [`stages/pr1-container-factory-explicit-methods.md`](./stages/pr1-container-factory-explicit-methods.md)
> - [`stages/pr1-cosmos-settings-required-episodes-container.md`](./stages/pr1-cosmos-settings-required-episodes-container.md)
> - [`stages/pr1-container-split-legacy-podcasts-episodes.md`](./stages/pr1-container-split-legacy-podcasts-episodes.md)
> - [`stages/pr1-di-explicit-podcast-repository-registration.md`](./stages/pr1-di-explicit-podcast-repository-registration.md)
> - [`stages/pr1-v2-models-and-podcast-repository.md`](./stages/pr1-v2-models-and-podcast-repository.md)
> - [`stages/pr1-legacy-to-v2-migration-service.md`](./stages/pr1-legacy-to-v2-migration-service.md)
> - [`stages/pr1-migration-service-moved-to-console-app.md`](./stages/pr1-migration-service-moved-to-console-app.md)
> - [`stages/pr1-expanded-container-settings-and-factory-methods.md`](./stages/pr1-expanded-container-settings-and-factory-methods.md)
> - [`stages/pr1-v2-repositories-subjects-discovery-activities-lookup.md`](./stages/pr1-v2-repositories-subjects-discovery-activities-lookup.md)
> - [`stages/pr1-pushsubscriptions-container-and-v2-repository.md`](./stages/pr1-pushsubscriptions-container-and-v2-repository.md)

## Phase 1: New Persistence Contracts

### `Class-Libraries/RedditPodcastPoster.Persistence.Abstractions/IPodcastRepository.cs`
- [ ] Remove episode-embedded query methods from podcast abstraction.
- [ ] Keep podcast-only CRUD/query operations.

### `Class-Libraries/RedditPodcastPoster.Persistence.Abstractions/IEpisodeRepository.cs` (new)
- [ ] Add episode-centric operations, for example:
  - [ ] `GetEpisode(...)`
  - [ ] `GetByPodcastId(...)`
  - [ ] `Save(...)`
  - [ ] `Delete(...)`
  - [ ] query methods replacing legacy `x.Episodes.Any(...)` patterns.

### `Class-Libraries/RedditPodcastPoster.Models/Podcast.cs`
- [ ] Remove `Episodes` member.

### `Class-Libraries/RedditPodcastPoster.Models/Episode.cs`
- [ ] Add `PodcastId` member.
- [ ] Ensure JSON metadata and persistence attributes are aligned with Cosmos model.
- [ ] Add/search-support denormalized fields needed by indexer query (`podcastName`, `podcastSearchTerms`, language strategy).
- [ ] Do not add duplicate compact identifier members for IDs already present (`spotifyId`, `youTubeId`, `appleId`).
- [ ] Add metadata sync marker for drift detection (for example `podcastMetadataVersion` or `podcastMetadataUpdatedAt`).

### `Class-Libraries/RedditPodcastPoster.Search/EpisodeSearchRecord.cs`
- [ ] Introduce reduced-key schema (`CompactSearchRecord`) for search index payload minimization.
- [ ] Keep schema version marker (for example `sv`) for UI compatibility.
- [ ] Remove full URL fields from indexed record.
- [ ] Map compact keys from existing episode IDs (`sid`/`yid`/`aid`) and derive Apple slug key (`as`) from Apple URL regex.

## Phase 2: Cosmos Repositories and Container Wiring

### `Class-Libraries/RedditPodcastPoster.Persistence/PodcastRepository.cs`
- [ ] Remove embedded-episode mutation logic.
- [ ] Keep podcast metadata operations only.

### `Class-Libraries/RedditPodcastPoster.Persistence/EpisodeRepository.cs` (new)
- [ ] Implement `IEpisodeRepository` against `Episodes`.
- [ ] Use partition key `/podcastId` for point and scoped queries.

### `Class-Libraries/RedditPodcastPoster.Persistence/FileRepository.cs`
- [ ] Align local/file persistence model to separate podcast and episode entities (if required for local tooling/tests).

### Composition roots (`Program.cs` and DI setup)
- [ ] Register `IEpisodeRepository`.
- [ ] Add feature flag support for model cutover.

## Phase 3: API/Core Refactor

### `Cloud/Api/Handlers/EpisodeHandler.cs`
- [ ] Replace podcast-embedded episode lookup and mutation.
- [ ] Use `IEpisodeRepository` for episode lifecycle operations.
- [ ] Use `IPodcastRepository` only for podcast metadata retrieval.

### `Cloud/Api/Handlers/PodcastHandler.cs`
- [ ] Replace `podcast.Episodes` usage for counts, selection, and indexing lists.
- [ ] Query episodes through `IEpisodeRepository` by `podcastId`.
- [ ] Trigger episode metadata fan-out updates when podcast properties affecting search are changed.

### `Class-Libraries/RedditPodcastPoster.EntitySearchIndexer/EpisodeSearchIndexerService.cs`
- [ ] Source episodes via `IEpisodeRepository`.
- [ ] Ensure index writes map to reduced-key `CompactSearchRecord` contract.

### `Class-Libraries/RedditPodcastPoster.Indexing/Indexer.cs`
- [ ] Refactor flows that assume embedded episodes.

### `Class-Libraries/RedditPodcastPoster.Common/Episodes/*`
- [ ] Migrate episode processing helpers to repository-driven access.

### `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs`
- [ ] Replace datasource query in `CreateDataSource` from embedded-episode join to `Episodes`-container query.
- [ ] Switch high-watermark semantics from `p._ts` to `e._ts`.
- [ ] Validate query field mapping still matches reduced-key `EpisodeSearchRecord` schema.

## Phase 4: UI and Contract Migration

### UI/search consumer
- [ ] Add support for `CompactSearchRecord` key names.
- [ ] Reconstruct URLs client-side from compact keys mapped from existing IDs (`sid`/`yid`/`aid`).
- [ ] Derive Apple slug (`as`) from Apple URL using regex when needed.
- [ ] Support schema version (`sv`) during transition.
- [ ] Keep backward compatibility with legacy search record keys during rollout window.

## Phase 5: Console/Processor Refactor

Update processors identified from code scan to stop using `podcast.Episodes`:

- [ ] `Console-Apps/AddAudioPodcast/AddAudioPodcastProcessor.cs`
- [ ] `Console-Apps/RemoveEpisodes/Processor.cs`
- [ ] `Console-Apps/UnremoveEpisodes/Processor.cs`
- [ ] `Console-Apps/EnrichPodcastWithImages/Processor.cs`
- [ ] `Console-Apps/EnrichYouTubeOnlyPodcasts/EnrichYouTubePodcastProcessor.cs`
- [ ] `Console-Apps/FixDatesFromApple/Processor.cs`
- [ ] `Console-Apps/Tweet/TweetProcessor.cs`
- [ ] `Console-Apps/KVWriter/KVWriterProcessor.cs`
- [ ] `Console-Apps/TextClassifierTraining/TrainingDataProcessor.cs`
- [ ] other remaining files returned by static scan for `.Episodes` usage.

## Phase 6: Migration Tooling

### New migration job (new processor/app)
- [ ] Read legacy `CultPodcasts` documents.
- [ ] Emit podcasts into `Podcasts`.
- [ ] Emit episodes into `Episodes` with `podcastId`.
- [ ] Populate denormalized episode metadata fields needed for search.
- [ ] Validate existing IDs (`spotifyId`, `youTubeId`, `appleId`) are populated for compact search mapping.
- [ ] Write reconciliation outputs (counts and mismatch details).

## Phase 7: Verification and Cutover Readiness

### Compile and static checks
- [ ] Build solution successfully after removing `Podcast.Episodes`.
- [ ] Ensure zero references to embedded-episode patterns in runtime paths.
- [ ] Ensure zero `JOIN e IN p.episodes` in search-index datasource definitions.

### Functional parity
- [ ] Validate publish/delete/unremove/index/tweet flows.
- [ ] Validate podcast retrieval and rename side effects.
- [ ] Validate search indexing continues to produce expected records after datasource query migration.
- [ ] Validate podcast metadata updates fan out to episodes and surface in search results.
- [ ] Validate URL reconstruction in UI from compact keys sourced from existing IDs and Apple URL regex slug derivation.

### Data parity
- [ ] Match podcast totals.
- [ ] Match episode totals.
- [ ] Match per-podcast episode counts.
- [ ] Match search index document totals and sampled search fields between old and new query model.

### Operational readiness
- [ ] Validate RU and latency profile on `Episodes`.
- [ ] Validate RU and latency profile of fan-out metadata updates.
- [ ] Validate search-index storage reduction after schema/key minimization.
- [ ] Confirm no writes reach legacy container post-cutover.
