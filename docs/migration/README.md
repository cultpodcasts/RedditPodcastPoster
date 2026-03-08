# Episode Detachment Migration - Entry Point

This is the single entrypoint for the migration from embedded `Podcast.Episodes` to discrete `Episode` documents with `podcastId` relationship in Cosmos DB.

## Read Order

1. **Strategy and constraints**
   - [`concrete-migration-plan-and-cutover-strategy.md`](./concrete-migration-plan-and-cutover-strategy.md)

2. **Repository-mapped execution checklist**
   - [`implementation-checklist-mapped-to-repo.md`](./implementation-checklist-mapped-to-repo.md)

3. **Delivery sequencing by PR**
   - [`sequenced-pr-plan.md`](./sequenced-pr-plan.md)

## Scope Summary

- Move from embedded `episodes` array in `Podcast` to discrete `Episode` entities.
- Persist episodes in `Episodes` container with partition key `/podcastId`.
- Persist podcasts in `Podcasts` container with partition key `/id`.
- Keep search-required podcast metadata denormalized on episode records.
- Preserve rollback safety with legacy `CultPodcasts` write-freeze and staged cutover.

## Critical Concern to Track

- Search index datasource query migration in:
  - `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs`
  - Method: `CreateDataSource`
- Replace embedded query shape (`JOIN e IN p.episodes`) with direct `Episodes` query.
- Ensure podcast metadata changes fan out to affected episodes so search fields stay consistent.

## Completion Gates

- No runtime dependency on `Podcast.Episodes`.
- Data parity validated (podcast count, episode count, per-podcast counts).
- Search index parity validated after datasource query migration.
- Production reads/writes on target relationship-model containers.
