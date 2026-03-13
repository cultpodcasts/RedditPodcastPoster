# Episode Detachment Migration - Entry Point

This is the single entrypoint for the migration from embedded `Podcast.Episodes` to discrete `Episode` documents with `podcastId` relationship in Cosmos DB.

## Current Implementation Progress

- Migration + sampled parity checks are implemented and passing.
- Search datasource migrated to detached episodes query (`FROM episodes e`) with `e._ts` high-watermark semantics.
- `podcastRemoved` hydration/filtering implemented for episode search filtering.
- Runtime update flow uses `PodcastUpdater` as the default `IPodcastUpdater` over detached episodes.
- Social + shortener contract chain uses detached episode pairs in all active runtime paths.
- `IPodcastRepository` (legacy) confined to `LegacyPodcastToV2Migration` only. All other apps use `IPodcastRepositoryV2`.
- `IPodcastEpisodeProvider` / `IPodcastEpisodePoster` evolved into canonical detached-model interfaces using the `PodcastEpisode` pair. Not retired — active primary interfaces.
- `ToLegacyPodcast`, `ToLegacyEpisode`, `.ToLegacy()` helpers removed from codebase.
- Factory overloads (`FindSpotifyEpisodeRequestFactory`, `FindAppleEpisodeRequestFactory`) fully on V2 model aliases.
- `CosmosDbDownloader --use-v2` implemented: downloads all V2 containers to local files.
- `CosmosDbUploader --use-v2` implemented: restores V2 local files to all V2 containers.
- `PublicDatabasePublisher --use-v2` implemented: publishes from V2 Podcasts + Episodes containers.
- Build is green.

## What remains

See [`remaining-work-audit.md`](./remaining-work-audit.md) for the authoritative current-state audit.

**Summary of open items:**
- ❌ No tests for any V2/detached-episode code paths
- ⏳ Phase 7 verification gates unrun (RU/latency, end-to-end flow validation, per-podcast count parity)
- 🟡 `ModelTransformProcessor` / `JsonSplitCosmosDbUploadProcessor` — assess for retirement
- ℹ️ `CompactSearchRecord` descoped; `Podcast.Episodes` intentionally retained on legacy model
- `IPodcastFilterV2`/`PodcastFilterV2` and `IPodcastEpisodeFilterV2`/`PodcastEpisodeFilterV2` deleted; V1 filter interfaces are canonical (already use V2 models).
- `PodcastEpisodeProvider` consolidated onto `IPodcastEpisodeFilter`.
