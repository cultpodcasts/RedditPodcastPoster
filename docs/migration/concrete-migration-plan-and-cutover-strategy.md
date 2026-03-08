# Concrete Migration Plan and Cutover Strategy

## 1. Target Data Model

### `Podcast` entity
- Keep podcast metadata only.
- Remove embedded `Episodes` collection.

### `Episode` entity
- Store each episode as a discrete document.
- Add foreign key `podcastId` (GUID) linking to `Podcast.id`.
- Keep current episode fields (`release`, `posted`, `tweeted`, `blueskyPosted`, etc.).
- Denormalize mutable podcast metadata needed for search (`podcastName`, `podcastSearchTerms`, language strategy).

## 2. Migration Phases

1. **Freeze legacy writes**
   - Set current `CultPodcasts` container to read-only at the application layer.
   - Allow reads for validation and rollback checks.

2. **Provision target containers**
   - `Podcasts` for podcast metadata.
   - `Episodes` for discrete episode documents.
   - Optional: `MigrationAudit` for parity and reconciliation artifacts.

3. **Implement repositories and feature flag**
   - Add separate repositories for podcasts and episodes.
   - Introduce toggle (for example `UseRelationshipModel`) to switch read/write paths.

4. **Run full backfill**
   - For each legacy podcast document:
     - Write podcast metadata to `Podcasts`.
     - Write each embedded episode to `Episodes` with `podcastId = podcast.id`.
     - Populate denormalized episode search fields (`podcastName`, `podcastSearchTerms`, language fallback).

5. **Reconciliation pass**
   - Validate:
     - Total podcast count.
     - Total episode count.
     - Per-podcast episode counts.
     - Sampled field parity for status flags and IDs.
     - Search field parity for denormalized metadata.

6. **Shadow-read validation**
   - In non-prod and then prod read-compare mode, compare legacy vs target-model responses for critical endpoints.

7. **Cutover**
   - Enable reads/writes to target containers.
   - Keep legacy container immutable during rollback window.

8. **Stabilization and retirement**
   - Monitor errors, RU consumption, and latency.
   - Remove legacy embedded-episode code path after signoff.

## 3. Cosmos DB Partition-Key Strategy

### `Podcasts`
- Partition key: `/id`
- Rationale: dominant access is point-read/update by podcast ID.

### `Episodes`
- Partition key: `/podcastId`
- Rationale: dominant access is by podcast (`list`, `filter`, `state updates`).

### Episode-by-ID lookup strategy
- Short term: cross-partition query by episode `id` when `podcastId` is unknown.
- Preferred: include `podcastId` in API contracts and internal commands.
- Optional optimization: small lookup container mapping `episodeId -> podcastId` to support point reads.

## 4. Cosmos DB Container Strategy

Use new containers instead of in-place schema mutation:
- `Podcasts`
- `Episodes`
- (Optional) `MigrationAudit`

Benefits:
- Safe parallel validation before cutover.
- Simplified rollback (flip feature flag to legacy reads).
- No partial-schema risk in existing `CultPodcasts` container.

## 5. Search Index Data Source Concern (`CreateDataSource`)

Current query assumes embedded episodes (`FROM podcasts p JOIN e IN p.episodes`). That query becomes invalid after detaching episodes.

### Required strategy
- Point the search index datasource container at `Episodes`.
- Query only episodes (`FROM episodes e`) with high-watermark on `e._ts`.
- Keep all search-required fields on `Episode` records:
  - `podcastName`
  - `podcastSearchTerms`
  - `lang` (direct or fallback value precomputed on episode)

### Operational implications
- Podcast metadata changes (for example rename/searchTerms changes) must trigger fan-out updates to affected episodes.
- Indexer change-detection policy remains `_ts`, now tracking episode document mutations.

## 6. Cutover and Rollback

### Cutover checklist
1. Confirm write freeze on legacy container.
2. Confirm backfill success and reconciliation pass.
3. Enable relationship-model flag in non-prod and validate.
4. Enable relationship-model flag in prod.
5. Verify no write traffic reaches legacy container.

### Rollback checklist
1. Disable relationship-model flag.
2. Return reads to legacy container (still read-only for episode writes if policy requires).
3. Investigate parity defects using reconciliation artifacts.
4. Re-run targeted migration/reconciliation before next cutover attempt.

## 7. Verification Gates

### Code verification
- Remove `Podcast.Episodes` property and resolve all compile errors.
- Replace embedded navigation/query patterns with episode-repository operations.
- Replace search index datasource query in `CreateSearchIndexProcessor.CreateDataSource` to episode-container query semantics.

### Static verification
- Zero usages of:
  - `podcast.Episodes`
  - predicates like `x.Episodes.Any(...)`
  - `JOIN e IN p.episodes` in search-index datasource definitions.

### Functional parity
- Verify API and processor workflows:
  - publish/tweet/bluesky episode
  - delete/unremove episode
  - indexing/search updates
  - podcast retrieval and rename side effects

### Data parity
- Match podcast count, episode count, and per-podcast counts.
- Validate sampled episode field equivalence (including posted/tweeted/removed flags).
- Validate search document parity (document count and sampled field parity) before and after query migration.
- Validate podcast metadata fan-out updates are applied to episodes and reflected in search output.
