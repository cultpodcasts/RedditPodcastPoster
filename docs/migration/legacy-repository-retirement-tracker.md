# Legacy IPodcastRepository Retirement - Migration Tracker

## Files to Migrate (Excluding LegacyPodcastToV2Migration)

### ✅ **Migrated (3)**
1. ✅ PodcastService.cs - Queries V2, converts to legacy at return
2. ✅ AddYouTubeChannelProcessor.cs - Uses V2 repository
3. ✅ AddAudioPodcastProcessor.cs - Already migrated earlier

### 🔄 **Active Production Services (High Priority - 15)**
4. BlueskyPoster.cs
5. EpisodeProcessor.cs
6. EpisodeResolver.cs
7. PodcastEpisodesPoster.cs
8. PodcastFactory.cs
9. EpisodeResultsEnricher.cs
10. EpisodeSearchIndexerService.cs
11. Indexer.cs
12. IndexablePodcastIdProvider.cs
13. NonPodcastServiceCategoriser.cs
14. PodcastsUpdater.cs
15. RecentPodcastEpisodeCategoriser.cs
16. SubjectFactory.cs
17. TweetPoster.cs
18. CategorisedItemProcessor.cs (legacy)
19. UrlSubmitter.cs (legacy)
20. PodcastsSubscriber.cs

### 🛠️ **Console Utility Apps (Medium Priority - 11)**
21. AddSubjectToSearchMatches/Processor.cs
22. CategorisePodcastEpisodes/CategorisePodcastEpisodesProcessor.cs
23. CosmosDbFixer/CosmosDbFixer.cs
24. DeleteSearchDocument/DeleteSearchDocumentProcessor.cs
25. EnrichEpisodesFromPostFlare/SubredditPostFlareEnricher.cs
26. EnrichYouTubeOnlyPodcasts/EnrichYouTubePodcastProcessor.cs
27. Index/IndexProcessor.cs
28. JsonSplitCosmosDbUploader/JsonSplitCosmosDbUploadProcessor.cs
29. RenamePodcast/RenamePodcastProcessor.cs
30. Sqllite3DatabasePublisher/Sqllite3DatabasePublisher.cs
31. WebsubStatus/WebSubStatusProcessor.cs
32. YouTubePushNotificationSubscribe/SubscribeProcessor.cs

### ⚠️ **Legacy Service Implementations (Do NOT Migrate)**
- PodcastRepository.cs - Legacy implementation
- PodcastUpdater.cs - Legacy implementation (replaced by V2)
- PodcastEpisodeProvider.cs - Legacy implementation (V2 exists)

---

## Migration Strategy

### Batch 1: Critical Production Services (5 files)
Focus on services used by API and main processing flows:
- EpisodeSearchIndexerService
- Indexer
- PodcastsUpdater
- PodcastFactory
- BlueskyPoster

### Batch 2: Episode/Posting Services (5 files)
- EpisodeProcessor
- EpisodeResolver
- PodcastEpisodesPoster
- TweetPoster
- CategorisedItemProcessor, UrlSubmitter (both have V2, can remove legacy)

### Batch 3: Discovery/Enrichment (5 files)
- EpisodeResultsEnricher
- IndexablePodcastIdProvider
- NonPodcastServiceCategoriser
- RecentPodcastEpisodeCategoriser
- SubjectFactory

### Batch 4: Console Utilities (11 files)
- All console app processors
- Lower priority (one-off tools)

### Batch 5: YouTube Subscriptions (1 file)
- PodcastsSubscriber

---

## Current Progress
- ✅ 3 of 32 migrated (9%)
- 🔄 29 remaining

---

Status: In Progress
