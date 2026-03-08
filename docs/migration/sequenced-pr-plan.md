# Sequenced PR Plan

## PR1 — New Model and Repository Foundations (No Behavior Switch)

### Scope
- Add `IEpisodeRepository` abstraction.
- Add `EpisodeRepository` Cosmos implementation for `Episodes` (partition key `/podcastId`).
- Remove `Episodes` from `Podcast`.
- Add `PodcastId` to `Episode`.
- Keep current runtime behavior gated so production path is not switched in this PR.

### Exit criteria
- Solution builds.
- New repository tests pass.
- No production behavior change yet.

## PR2 — API, Core Handler, and Search Datasource Migration

### Scope
Refactor to use repository relationship model in:
- `Cloud/Api/Handlers/EpisodeHandler.cs`
- `Cloud/Api/Handlers/PodcastHandler.cs`
- `Class-Libraries/RedditPodcastPoster.EntitySearchIndexer/EpisodeSearchIndexerService.cs`
- `Class-Libraries/RedditPodcastPoster.Indexing/Indexer.cs`
- `Console-Apps/CreateSearchIndex/CreateSearchIndexProcessor.cs` (`CreateDataSource` query migration)

Key changes:
- Replace `podcast.Episodes` reads/mutations with `IEpisodeRepository` queries/commands.
- Replace search datasource query from embedded-episode join (`JOIN e IN p.episodes`) to `Episodes` container query.
- Move high-watermark semantics from podcast timestamp to episode timestamp (`e._ts`).
- Introduce/enable feature flag (for example `UseRelationshipModel`) for controlled rollout.

### Exit criteria
- API parity tests pass in legacy and relationship-model modes.
- Critical episode workflows function with new repositories.
- Search indexer produces expected records with the migrated datasource query.

## PR3 — Console and Processor Migration

### Scope
Migrate processor/tooling code paths from embedded episodes to `IEpisodeRepository`, including high-impact processors:
- `AddAudioPodcast`
- `RemoveEpisodes`
- `UnremoveEpisodes`
- `Tweet`
- `Enrich*`
- `FixDatesFromApple`
- `KVWriter`
- `TextClassifierTraining`

### Exit criteria
- Processor smoke tests pass.
- Runtime paths no longer rely on `Podcast.Episodes`.

## PR4 — Data Migration and Production Cutover

### Scope
- Add migration job from legacy `CultPodcasts` to `Podcasts` + `Episodes`.
- Freeze writes to legacy container.
- Run backfill and reconciliation.
- Enable relationship-model flag in production.
- Keep legacy container immutable for rollback window.

### Exit criteria
- Data parity checks pass.
- Production reads/writes use target containers.
- Rollback procedure documented and tested.

## Release Gates for PR4

- Zero runtime references to `Podcast.Episodes`.
- Functional parity verified for publish/delete/index/rename/tweet flows.
- Search datasource parity verified (document counts and sampled fields).
- RU and latency within acceptable thresholds.
- Legacy container receives no writes after cutover.

## Rollback Strategy

1. Disable relationship-model flag.
2. Restore reads to legacy path (if needed).
3. Analyze mismatches using reconciliation outputs.
4. Fix and rerun targeted migration before next cutover attempt.
