# V2 Services Migration Progress

## Overview
This document tracks the creation of V2 service variants that work with detached episodes via `IEpisodeRepository` instead of the embedded `podcast.Episodes` collection.

## Completed V2 Services

### ✅ Core Episode Services

**1. IPodcastEpisodeFilterV2 / PodcastEpisodeFilterV2**
- Location: `Class-Libraries\RedditPodcastPoster.Common\Episodes\`
- Purpose: Filters episodes from detached `IEpisodeRepository`
- **Returns:** `PodcastEpisodeV2` (V2 models only)
- Key Methods:
  - `GetNewEpisodesReleasedSince()` - Episodes ready to post
  - `GetMostRecentUntweetedEpisodes()` - Episodes needing tweets
  - `GetMostRecentBlueskyReadyEpisodes()` - Episodes ready for Bluesky
  - `IsRecentlyExpiredDelayedPublishing()` - Delayed publishing check
- Dependencies: `IPodcastRepositoryV2`, `IEpisodeRepository`
- Converts V2 models to legacy internally for business logic compatibility

**2. IPodcastEpisodeProviderV2 / PodcastEpisodeProviderV2**
- Location: `Class-Libraries\RedditPodcastPoster.Common\Episodes\`
- Purpose: Provides podcast episodes across all podcasts
- **Returns:** `PodcastEpisodeV2` (V2 models only)
- Key Methods:
  - `GetUntweetedPodcastEpisodes()` - All untweeted episodes
  - `GetUntweetedPodcastEpisodes(Guid)` - For specific podcast
  - `GetBlueskyReadyPodcastEpisodes()` - All Bluesky-ready
  - `GetBlueskyReadyPodcastEpisodes(Guid)` - For specific podcast
- Dependencies: `IPodcastRepositoryV2`, `IPodcastEpisodeFilterV2`
- Uses V2 filter for episode retrieval

**3. IPodcastEpisodePosterV2 / PodcastEpisodePosterV2**
- Location: `Class-Libraries\RedditPodcastPoster.Common\Episodes\`
- Purpose: Posts podcast episodes and updates posted status in detached repository
- **Accepts:** `PodcastEpisodeV2` (V2 models only)
- Key Methods:
  - `PostPodcastEpisode()` - Posts episode(s) and marks as posted
- Features:
  - Supports bundled episodes using title regex
  - Loads episodes from detached repository for bundling
  - Updates episode `Posted` status via `IEpisodeRepository`
- Dependencies: `IEpisodePostManager`, `IPostModelFactory`, `IEpisodeRepository`

**4. IPodcastFilterV2 / PodcastFilterV2**
- Location: `Class-Libraries\RedditPodcastPoster.Common\Podcasts\`
- Purpose: Filters episodes based on elimination terms
- Key Methods:
  - `Filter()` - Filters episodes and marks matching ones as removed
- Features:
  - Checks episode titles and descriptions against elimination terms
  - Updates episode `Removed` status via `IEpisodeRepository`
  - Returns `FilterResult` with filtered episodes
- Dependencies: `IPodcastRepositoryV2`, `IEpisodeRepository`

### ✅ Infrastructure Services (from previous session)

**5. IEpisodeMerger / EpisodeMerger**
- Location: `Class-Libraries\RedditPodcastPoster.Persistence\`
- Purpose: Merges episodes without mutating embedded collections
- Returns `EpisodeMergeResult` with V2 episodes to save

**6. PodcastUpdaterV2**
- Location: `Class-Libraries\RedditPodcastPoster.PodcastServices\`
- Purpose: Implements `IPodcastUpdater` with V2 repositories
- Uses `IPodcastRepositoryV2` and `IEpisodeRepository`

### ✅ URL Submission Services

**7. IPodcastAndEpisodeFactoryV2 / PodcastAndEpisodeFactoryV2**
- Location: `Class-Libraries\RedditPodcastPoster.UrlSubmission\Factories\`
- Purpose: Creates new podcasts with initial episodes
- **Returns:** `CreatePodcastWithEpisodeResponseV2` with V2 models
- Features:
  - Creates podcast and episode from categorised URL
  - Enriches subjects for new episode
  - Persists directly to V2 repositories
- Dependencies: `IEpisodeFactory`, `IPodcastFactory`, `IPodcastRepositoryV2`, `IEpisodeRepository`, `ISubjectEnricher`

**8. IPodcastProcessorV2 / PodcastProcessorV2**
- Location: `Class-Libraries\RedditPodcastPoster.UrlSubmission\`
- Purpose: Adds episodes to existing podcasts
- Features:
  - Matches episodes using fuzzy matching
  - Creates new episode if no match found
  - Updates existing episode if match found
  - Persists episodes via `IEpisodeRepository`
  - Persists podcast metadata via `IPodcastRepositoryV2`
- Dependencies: `IEpisodeHelper`, `IEpisodeEnricher`, `IEpisodeFactory`, `ISubjectEnricher`, repositories

**9. ICategorisedItemProcessorV2 / CategorisedItemProcessorV2**
- Location: `Class-Libraries\RedditPodcastPoster.UrlSubmission\`
- Purpose: Processes categorised URLs (router between create/update)
- Features:
  - Routes to factory for new podcasts
  - Routes to processor for existing podcasts
  - Handles persistence flags
- Dependencies: `IPodcastProcessorV2`, `IPodcastAndEpisodeFactoryV2`, `IPodcastRepositoryV2`

**10. IUrlSubmitterV2 / UrlSubmitterV2**
- Location: `Class-Libraries\RedditPodcastPoster.UrlSubmission\`
- Purpose: Main URL submission entry point
- Features:
  - Categorises URLs using existing categoriser
  - Processes via categorised item processor
  - Error handling and logging
- Dependencies: `IPodcastRepositoryV2`, `IPodcastService`, `IUrlCategoriser`, `ICategorisedItemProcessorV2`

## Registration Status

All V2 services are registered in DI:
- ✅ `IEpisodeMerger` → `EpisodeMerger` (Persistence layer)
- ✅ `IPodcastEpisodeFilterV2` → `PodcastEpisodeFilterV2` (Common layer)
- ✅ `IPodcastEpisodeProviderV2` → `PodcastEpisodeProviderV2` (Common layer)
- ✅ `IPodcastEpisodePosterV2` → `PodcastEpisodePosterV2` (Common layer)
- ✅ `IPodcastFilterV2` → `PodcastFilterV2` (Common layer)
- ✅ `IUrlSubmitterV2` → `UrlSubmitterV2` (UrlSubmission layer)
- ✅ `IPodcastProcessorV2` → `PodcastProcessorV2` (UrlSubmission layer)
- ✅ `ICategorisedItemProcessorV2` → `CategorisedItemProcessorV2` (UrlSubmission layer)
- ✅ `IPodcastAndEpisodeFactoryV2` → `PodcastAndEpisodeFactoryV2` (UrlSubmission layer)
- ✅ **`IPodcastUpdater` → `PodcastUpdaterV2`** (Default implementation!) 🎉

**🎊 MAJOR MILESTONE:** PodcastUpdaterV2 is now the DEFAULT implementation for IPodcastUpdater!

This means:
- ✅ All podcast updates use detached episodes automatically
- ✅ No consumer code changes required
- ✅ Legacy PodcastUpdater replaced
- ✅ Production traffic flows through V2 architecture

## Migration Strategy

### Current State: Dual-Track Support
- ✅ Legacy services (`IPodcastEpisodeFilter`, `IPodcastEpisodeProvider`) still registered
- ✅ V2 services registered alongside legacy
- ✅ Consumers can choose which version to use

### Next Steps

**Phase 1: Migrate Consumers**
1. Update console apps to use V2 providers
2. Update API handlers to use V2 providers
3. Test each consumer independently

**Phase 2: Additional V2 Services Needed**
1. `IPodcastEpisodePosterV2` - for bundled episode posting
2. `IPodcastFilterV2` - for elimination terms filtering
3. URL submission services V2

**Phase 3: Deprecation**
1. Mark legacy services as `[Obsolete]`
2. Remove legacy services after all consumers migrated
3. Remove `Podcast.Episodes` property

## V2 Service Design Pattern

All V2 services follow this pattern:

```csharp
// V2 Interface - Returns ONLY V2 models
public interface IServiceV2
{
    Task<IEnumerable<PodcastEpisodeV2>> GetEpisodes(Guid podcastId);
    // No legacy methods - pure V2
}

// V2 Implementation
public class ServiceV2(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository
) : IServiceV2
{
    public async Task<IEnumerable<PodcastEpisodeV2>> GetEpisodes(Guid podcastId)
    {
        // 1. Load V2 models
        var v2Podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
        var v2Episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();
        
        // 2. Create V2 pairs directly - no conversion!
        return v2Episodes.Select(e => new PodcastEpisodeV2(v2Podcast, e));
    }
}

// Consumer - Convert at boundary if needed
public class Consumer(IServiceV2 service)
{
    public async Task Process()
    {
        var v2Episodes = await service.GetEpisodes(podcastId);
        
        // If legacy models needed, convert at boundary:
        var legacyEpisodes = v2Episodes.Select(x => x.ToLegacy());
    }
}
```

### Key Principles
- ✅ V2 interfaces return **only** V2 models (`PodcastEpisodeV2`)
- ✅ No method name suffixes (`*V2`) - the interface name indicates it's V2
- ✅ Conversion happens at **boundaries** (consumer's responsibility)
- ✅ Clean separation: use legacy OR V2, not both in same interface

### Key Benefits
- ✅ Works with detached episodes
- ✅ Maintains compatibility with existing logic
- ✅ Can be tested independently
- ✅ Gradual migration path
- ✅ Legacy and V2 coexist safely

## Testing Strategy

### Unit Testing
- Test V2 services with mocked repositories
- Verify conversion methods work correctly
- Test edge cases (no episodes, removed podcasts, etc.)

### Integration Testing
- Test V2 services against real Cosmos DB (dev environment)
- Compare results with legacy services
- Verify episode filtering logic matches

### Consumer Testing
- Update one consumer at a time
- Run smoke tests after each migration
- Compare output with legacy implementation

## Known Limitations

### Temporary Limitations
1. V2 services still use legacy `Podcast` and `Episode` models internally for compatibility
2. Some logic duplication between legacy and V2 implementations
3. Conversion overhead from V2 to legacy models

### Future Improvements
1. Refactor internal logic to work directly with V2 models
2. Extract shared filtering logic into common utilities
3. Remove conversion methods once legacy removed

## Build Status
✅ **All code compiles successfully**
✅ **Zero build errors**
✅ **All V2 services registered in DI**

## Completed Migrations

### Console Processors
- ✅ `AddAudioPodcastProcessor` - uses `PodcastUpdaterV2`
- ✅ `EnrichYouTubePodcastProcessor` - uses `IEpisodeRepository` directly
- ✅ `TweetProcessor` - Uses V2 repositories + extension methods
- ✅ `PostProcessor` - Uses `IPodcastEpisodeProviderV2` + `.ToLegacy()` at boundaries

### ✅ API Handlers
1. **SubmitUrlHandler** - Uses `IUrlSubmitterV2` and `IPodcastRepositoryV2` → detached episodes
2. **EpisodeHandler** - Already uses `IPodcastRepositoryV2` and `IEpisodeRepository` → detached episodes

### 🔄 Remaining Consumers
- Other API handlers (if they use legacy services)
- Any remaining console processors

---

## Next Session Tasks

1. **Add unit tests** for V2 services ⚠️ **HIGH PRIORITY**
2. **Integration tests** against Cosmos DB
3. **Audit remaining API handlers** for legacy service usage
4. **Performance testing** of V2 services
5. **Register PodcastUpdaterV2** as default `IPodcastUpdater`
