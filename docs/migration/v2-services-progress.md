# V2 Services Migration Progress

## Overview
This document tracks the creation and adoption of detached-episode services via `IEpisodeRepository`.

## Current status update
- Runtime default updater is now `PodcastUpdater` (detached episode flow).
- Historical references to `PodcastUpdaterV2` as default should be treated as milestone history, not current runtime state.
- Social + shortener contract chain now uses `PodcastEpisodeV2` end-to-end in active runtime paths.
- Current decommission focus is removal of remaining compatibility helpers/overloads and retirement of legacy duplicate service variants.

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

**6. PodcastUpdater (default updater)**
- Location: `Class-Libraries\RedditPodcastPoster.PodcastServices\`
- Purpose: Implements `IPodcastUpdater` with detached episode repositories
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
- ✅ **`IPodcastUpdater` → `PodcastUpdater`** (current default)

## Migration Strategy

### Current State: Decommission Phase
- ✅ Detached-episode services are active across core runtime flows.
- ✅ Social + shortener boundaries have been migrated to `PodcastEpisodeV2` contracts.
- 🔄 Active work is removing compatibility helpers/overloads and retiring legacy duplicate variants.

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
- ✅ Interfaces and implementations should converge on detached-episode contracts.
- ✅ Conversion happens only at explicit boundaries that are still pending decommission.
- ✅ Current migration goal is removal of those boundaries.

### Key Benefits
- ✅ Works with detached episodes
- ✅ Maintains compatibility with existing logic during transition
- ✅ Can be tested independently
- ✅ Clear path to final legacy runtime decommission

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
1. Some compatibility helpers still exist during final decommission sequencing.
2. Some logic duplication remains between legacy and detached implementations.

### Future Improvements
1. Remove remaining compatibility helpers once last callers are migrated.
2. Retire legacy duplicate service variants.
3. Remove conversion methods once legacy runtime usage is fully constrained to `PodcastRepository` and `LegacyPodcastToV2Migration`.

## Build Status
✅ **All code compiles successfully**
✅ **Zero build errors**
✅ **All V2 services registered in DI**

## Completed Migrations

### Console Processors
- ✅ `AddAudioPodcastProcessor` - uses detached repositories
- ✅ `EnrichYouTubePodcastProcessor` - uses `IEpisodeRepository` directly
- ✅ `TweetProcessor` - Uses detached repositories and detached pair contracts
- ✅ `PostProcessor` - Uses detached episode provider and detached social/shortener contracts

### ✅ API Handlers
1. **SubmitUrlHandler** - Uses detached repositories/services
2. **EpisodeHandler** - Uses detached episode + podcast repositories and detached social/shortener contracts

### 🔄 Remaining Consumers
- Any remaining runtime callers of legacy duplicate provider/poster services
- Any remaining callers of temporary compatibility overloads

---

## Next Session Tasks

1. Remove temporary compatibility overloads (`FindSpotifyEpisodeRequestFactory`, `FindAppleEpisodeRequestFactory`)
2. Remove obsolete conversion helpers when no longer referenced
3. Retire legacy provider/poster duplicate services
4. Add unit and integration coverage for decommissioned paths
5. Continue `CompactSearchRecord` rollout work
