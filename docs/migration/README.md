# Episode Detachment Migration - Entry Point

This is the single entrypoint for the migration from embedded `Podcast.Episodes` to discrete `Episode` documents with `podcastId` relationship in Cosmos DB.

## Current Implementation Progress

- Migration + sampled parity checks are implemented and passing.
- Search datasource has been migrated to detached episodes query (`FROM episodes e`) with `e._ts` high-watermark semantics.
- `podcastRemoved` hydration/filtering is implemented for episode search filtering.
- `PodcastHandler` now uses the target podcast repository and detached-episode fan-out updates through `IEpisodeRepository`.
- Remaining work is focused on full runtime removal of embedded-episode assumptions and reduced-key search contract rollout (`CompactSearchRecord`).

## Read Order

1. **📊 Current Status** (Start here for current state)
   - [`complete-status-report.md`](./complete-status-report.md) - **COMPREHENSIVE STATUS** - Complete achievement summary

2. **Strategy and constraints**
   - [`concrete-migration-plan-and-cutover-strategy.md`](./concrete-migration-plan-and-cutover-strategy.md)

3. **Repository-mapped execution checklist**
   - [`implementation-checklist-mapped-to-repo.md`](./implementation-checklist-mapped-to-repo.md)

4. **Parallel infrastructure rollout (deploy + migration + cutover)**
   - [`parallel-infrastructure-rollout-checklist.md`](./parallel-infrastructure-rollout-checklist.md)

5. **V2 Services Documentation** (Current Implementation)
   - [`v2-implementation-index.md`](./v2-implementation-index.md) - Quick reference table
   - [`v2-services-progress.md`](./v2-services-progress.md) - Implementation progress
   - [`v2-services-reference.md`](./v2-services-reference.md) - Complete API reference
   - [`podcast-episode-v2-guide.md`](./podcast-episode-v2-guide.md) - PodcastEpisodeV2 usage
   - [`url-submission-v2-complete.md`](./url-submission-v2-complete.md) - URL submission docs
   - [`architectural-cleanup-summary.md`](./architectural-cleanup-summary.md) - Design decisions

6. **Session Summaries**
   - [`final-session-summary.md`](./final-session-summary.md) - Latest session achievements

7. **Delivery sequencing by PR**
   - [`sequenced-pr-plan.md`](./sequenced-pr-plan.md)

## Stage Notes

- PR1: [`stages/pr1-modeltype-immutability.md`](./stages/pr1-modeltype-immutability.md)
- PR1: [`stages/pr1-episode-repository-scaffold.md`](./stages/pr1-episode-repository-scaffold.md)
- PR1: [`stages/pr1-episode-repository-podcastid-partition.md`](./stages/pr1-episode-repository-podcastid-partition.md)
- PR1: [`stages/pr1-container-factory-explicit-methods.md`](./stages/pr1-container-factory-explicit-methods.md)
- PR1: [`stages/pr1-cosmos-settings-required-episodes-container.md`](./stages/pr1-cosmos-settings-required-episodes-container.md)
- PR1: [`stages/pr1-container-split-legacy-podcasts-episodes.md`](./stages/pr1-container-split-legacy-podcasts-episodes.md)
- PR1: [`stages/pr1-di-explicit-podcast-repository-registration.md`](./stages/pr1-di-explicit-podcast-repository-registration.md)
- PR1: [`stages/pr1-v2-models-and-podcast-repository.md`](./stages/pr1-v2-models-and-podcast-repository.md)
- PR1: [`stages/pr1-legacy-to-v2-migration-service.md`](./stages/pr1-legacy-to-v2-migration-service.md)
- PR1: [`stages/pr1-migration-service-moved-to-console-app.md`](./stages/pr1-migration-service-moved-to-console-app.md)
- PR1: [`stages/pr1-expanded-container-settings-and-factory-methods.md`](./stages/pr1-expanded-container-settings-and-factory-methods.md)
- PR1: [`stages/pr1-v2-repositories-subjects-discovery-activities-lookup.md`](./stages/pr1-v2-repositories-subjects-discovery-activities-lookup.md)
- PR1: [`stages/pr1-pushsubscriptions-container-and-v2-repository.md`](./stages/pr1-pushsubscriptions-container-and-v2-repository.md)

## Scope Summary

- Move from embedded `episodes` array in `Podcast` to discrete `Episode` entities.
- Persist episodes in `Episodes` container with partition key `/podcastId`.
- Persist podcasts in `Podcasts` container with partition key `/id`.
- Persist lookup data in `LookUps` container (including typed `KnownTerms` and `EliminationTerms`).
- Persist subscriptions in dedicated `PushSubscriptions` container.
- Use explicit factory methods `CreatePodcastsContainer()` and `CreateEpisodesContainer()` for primary relationship-model containers.
- Keep search-required podcast metadata denormalized on episode records.
- Reduce search-index payload with compact keys mapped from existing episode IDs (`spotifyId`, `youTubeId`, `appleId`).
- Use `CompactSearchRecord` as the reduced-key search payload contract.
- Derive Apple slug from Apple URL by regex when needed.
- Preserve rollback safety with legacy `CultPodcasts` write-freeze and staged cutover.

## Critical Concerns to Track

- Search index datasource query migration in:
  - `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs`
  - Method: `CreateDataSource`
- Replace embedded query shape (`JOIN e IN p.episodes`) with direct `Episodes` query.
- Ensure podcast metadata changes fan out to affected episodes so search fields stay consistent.
- Ensure UI consumers support reduced-key `CompactSearchRecord` and URL reconstruction from compact keys.

## Completion Gates

- No runtime dependency on `Podcast.Episodes`.
- Data parity validated (podcast count, episode count, per-podcast counts).
- Search index parity validated after datasource query migration.
- Search-index storage reduction validated.
- UI compatibility validated for `CompactSearchRecord` reduced-key search record contract.
- `LookUps` container in use for `KnownTerms` and `EliminationTerms`.
- Dedicated `PushSubscriptions` container in use.
- Explicit container factory methods `CreatePodcastsContainer()` and `CreateEpisodesContainer()` are in use.
- Production reads/writes on target relationship-model containers.
