# Legacy IPodcastRepository Retirement - Migration Tracker

> Last verified against codebase: feature/my-diversion-then-ai-bringing-back-in
>
> **NOTE:** The original progress figure of "3 of 32 migrated" was incorrect.
> The actual migration happened through a different path than this tracker anticipated —
> `IPodcastRepositoryV2` was adopted directly rather than file-by-file. The list below
> reflects the verified current state.

---

## ✅ Confirmed: `IPodcastRepository` confined to allowed boundaries

`IPodcastRepository` (legacy) now appears only in:
- `IPodcastRepository.cs` — interface definition
- `PodcastRepository.cs` — legacy implementation (intentionally retained)
- `LegacyPodcastToV2MigrationProcessor.cs` — migration tool only
- `ServiceCollectionExtensions.cs` — `AddLegacyPodcastRepository()` method (only called from
  `LegacyPodcastToV2Migration/Program.cs`)

No production app, API handler, or active service registers or uses `IPodcastRepository`.

---

## ✅ All production/service files use `IPodcastRepositoryV2`

The following files from the original watchlist are confirmed to use `IPodcastRepositoryV2`:

**Active Production Services**
- ✅ BlueskyPostManager.cs
- ✅ CategorisedItemProcessor.cs
- ✅ DiscoveryResultsService.cs
- ✅ EpisodeHandler.cs
- ✅ EpisodeResolver.cs
- ✅ EpisodeResultsEnricher.cs
- ✅ EpisodeSearchIndexerService.cs
- ✅ HomepagePublisher.cs
- ✅ IndexablePodcastIdProvider.cs
- ✅ Indexer.cs
- ✅ NonPodcastServiceCategoriser.cs
- ✅ PodcastEpisodeProvider.cs (canonical detached interface — not legacy)
- ✅ PodcastEpisodesPoster.cs
- ✅ PodcastFactory.cs
- ✅ PodcastHandler.cs
- ✅ PodcastService.cs
- ✅ PodcastsSubscriber.cs
- ✅ PodcastsUpdater.cs
- ✅ PostProcessor.cs
- ✅ PublicHandler.cs
- ✅ RecentPodcastEpisodeCategoriser.cs
- ✅ SubjectFactory.cs
- ✅ SubmitUrlHandler.cs
- ✅ TweetProcessor.cs
- ✅ UrlSubmitter.cs

**Console Utility Apps**
- ✅ AddAudioPodcastProcessor.cs
- ✅ AddYouTubeChannelProcessor.cs
- ✅ CategorisePodcastEpisodesProcessor.cs
- ✅ CosmosDbFixer.cs
- ✅ DeleteSearchDocumentProcessor.cs
- ✅ EnrichPodcastEpisodesProcessor.cs
- ✅ EnrichYouTubePodcastProcessor.cs
- ✅ IndexAllEpisodesAuditProcessor.cs
- ✅ IndexProcessor.cs
- ✅ JsonSplitCosmosDbUploadProcessor.cs
- ✅ KVWriterProcessor.cs
- ✅ RenamePodcastProcessor.cs
- ✅ Sqllite3DatabasePublisher.cs
- ✅ SubredditPostFlareEnricher.cs
- ✅ SubscribeProcessor.cs
- ✅ TrainingDataProcessor.cs
- ✅ WebSubStatusProcessor.cs

---

## ⚠️ Intentionally retained legacy implementations

- `PodcastRepository.cs` — legacy implementation; source for `LegacyPodcastToV2Migration` only.
- `PodcastUpdater.cs` — superseded by new `PodcastUpdater` (V2-based); retained as rollback option.

---

## Status: ✅ Complete
