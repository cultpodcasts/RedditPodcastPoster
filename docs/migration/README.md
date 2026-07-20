# Episode Detachment Migration — Entry Point

This folder documents the migration from embedded `Podcast.Episodes` to discrete `Episode` documents with `podcastId` in Cosmos DB.

> **Migration complete.** Legacy persistence and `*V2` repository naming have been retired.
> Runtime code uses `RedditPodcastPoster.Persistence` with split containers only.
> Files named `v2-*` and content referencing `IPodcastRepositoryV2` / `PodcastEpisodeV2` are historical.

## Current architecture

- **Cosmos DB:** split containers (`Podcasts`, `Episodes`, `Subjects`, `Discovery`, `LookUps`, `Activity`, `PushSubscriptions`).
- **Repositories:** `IPodcastRepository`, `IEpisodeRepository`, `ISubjectRepository`, `IDiscoveryResultsRepository`, `ILookupRepository`, `IActivityRepository`, `IPushSubscriptionRepository`.
- **Episode pair type:** `PodcastEpisode` (`Podcast` + `Episode`).
- **Update pipeline:** `PodcastUpdater` as default `IPodcastUpdater`.
- **Search:** datasource queries detached `Episodes`; `EpisodeSearchIndexerService` uses detached repositories.
- **Configuration:** binds to `cosmosdb` section (see README).

## What remains (non-blocking)

See [`remaining-work-audit.md`](./remaining-work-audit.md) for test gaps and optional cleanup items.

**Summary:**
- ❌ No automated tests for detached-episode paths
- ✅ Legacy-format console tools (`ModelTransformer`, `JsonSplitCosmosDbUploader`) removed

## Historical docs

Stage plans, V2 service references, and cutover checklists under this folder are archived migration notes. Do not treat `*V2` interface names in those files as current API surface.
