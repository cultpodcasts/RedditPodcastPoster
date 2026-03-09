# V2 Services - Complete Reference Guide

## Overview
This guide provides a complete reference for all V2 services created to support the detached episode architecture.

## Architecture Pattern

All V2 services follow a consistent pattern:

```
┌─────────────────────────────────────────────────────────┐
│                    V2 Service Layer                      │
├─────────────────────────────────────────────────────────┤
│  - Accepts Guid podcastId (not Podcast object)          │
│  - Loads data from IPodcastRepositoryV2 & IEpisodeRepository │
│  - Converts V2 → Legacy for business logic compatibility│
│  - Performs operations on legacy models                  │
│  - Converts results back to V2                          │
│  - Persists changes via V2 repositories                 │
└─────────────────────────────────────────────────────────┘
         ↓                                    ↑
         ↓                                    ↑
┌────────────────────┐            ┌──────────────────────┐
│ IPodcastRepositoryV2│            │  IEpisodeRepository  │
│  (Podcasts)        │            │    (Episodes)        │
└────────────────────┘            └──────────────────────┘
```

## Service Catalog

### 1. Episode Filtering Services

#### IPodcastEpisodeFilterV2
**Purpose:** Filter episodes based on various criteria (tweets, Bluesky, posting)
**Returns:** `PodcastEpisodeV2` (V2 models only - no legacy methods)

**Methods:**
```csharp
Task<IEnumerable<PodcastEpisodeV2>> GetNewEpisodesReleasedSince(
    Guid podcastId, DateTime since, bool youTubeRefreshed, bool spotifyRefreshed);

Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodes(
    Guid podcastId, int numberOfDays);

Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentBlueskyReadyEpisodes(
    Guid podcastId, int numberOfDays);

bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode);
```

**Usage Example:**
```csharp
public class MyService(IPodcastEpisodeFilterV2 filter)
{
    public async Task ProcessPodcast(Guid podcastId)
    {
        // Returns V2 models
        var v2Episodes = await filter.GetMostRecentUntweetedEpisodes(podcastId, 7);
        
        // Convert to legacy if needed at boundary
        var legacyEpisodes = v2Episodes.Select(x => x.ToLegacy());
    }
}
```

---

### 2. Episode Provider Services

#### IPodcastEpisodeProviderV2
**Purpose:** Provide episodes across podcasts or for specific podcast
**Returns:** `PodcastEpisodeV2` (V2 models only - no legacy methods)

**Methods:**
```csharp
Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
    bool youTubeRefreshed, bool spotifyRefreshed);

Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(Guid podcastId);

Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
    bool youTubeRefreshed, bool spotifyRefreshed);

Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(Guid podcastId);
```

**Usage Example:**
```csharp
public class TweetService(IPodcastEpisodeProviderV2 provider)
{
    public async Task TweetNewEpisodes()
    {
        // Returns V2 models
        var v2Episodes = await provider.GetUntweetedPodcastEpisodes(
            youTubeRefreshed: true, 
            spotifyRefreshed: true);
        
        foreach (var v2Episode in v2Episodes)
        {
            // Work with V2 models directly, or convert if needed
            var legacy = v2Episode.ToLegacy();
        }
    }
}
```

---

### 3. Episode Posting Services

#### IPodcastEpisodePosterV2
**Purpose:** Post episodes to Reddit and update posted status
**Accepts:** `PodcastEpisodeV2` (V2 models only - no legacy methods)

**Methods:**
```csharp
Task<ProcessResponse> PostPodcastEpisode(
    PodcastEpisodeV2 podcastEpisode, 
    bool preferYouTube = false);
```

**Features:**
- ✅ Handles bundled episodes automatically
- ✅ Uses title regex to find related parts
- ✅ Posts all parts together
- ✅ Updates `Posted` flag for all parts
- ✅ Persists changes via `IEpisodeRepository`

**Usage Example:**
```csharp
public class PostingService(
    IPodcastEpisodeProviderV2 provider,
    IPodcastEpisodePosterV2 poster)
{
    public async Task PostNewEpisodes()
    {
        // Provider returns V2 models
        var v2Episodes = await provider.GetUntweetedPodcastEpisodes(true, true);
        
        foreach (var v2Episode in v2Episodes)
        {
            // Poster accepts V2 models directly
            var result = await poster.PostPodcastEpisode(v2Episode, preferYouTube: false);
            if (result.Success)
            {
                // Episode posted and marked!
            }
        }
    }
}
```

---

### 4. Episode Filtering by Terms

#### IPodcastFilterV2
**Purpose:** Filter episodes based on elimination terms

**Methods:**
```csharp
Task<FilterResult> Filter(Guid podcastId, List<string> eliminationTerms);
```

**Features:**
- ✅ Checks titles and descriptions
- ✅ Marks matching episodes as `Removed`
- ✅ Returns list of filtered episodes with matched terms
- ✅ Persists changes via `IEpisodeRepository`

**Usage Example:**
```csharp
public class ContentModerationService(
    IPodcastFilterV2 filter,
    IEliminationTermsProvider termsProvider)
{
    public async Task FilterPodcast(Guid podcastId)
    {
        var terms = await termsProvider.GetEliminationTerms();
        var result = await filter.Filter(podcastId, terms.Terms);
        
        if (result.FilteredEpisodes.Any())
        {
            // Log filtered episodes...
        }
    }
}
```

---

### 5. Episode Merging Services

#### IEpisodeMerger
**Purpose:** Merge new episodes with existing episodes

**Methods:**
```csharp
Task<EpisodeMergeResult> MergeEpisodes(
    Podcast podcast,
    IEnumerable<Episode> existingEpisodes,
    IEnumerable<Episode> episodesToMerge);
```

**Returns:**
```csharp
public record EpisodeMergeResult(
    IList<V2Episode> EpisodesToSave,      // V2 episodes ready to save
    IList<Episode> AddedEpisodes,         // Newly added episodes
    IList<(Episode, Episode)> MergedEpisodes,  // Updated episodes
    IList<IEnumerable<Episode>> FailedEpisodes // Ambiguous matches
);
```

**Usage Example:**
```csharp
public class UpdateService(
    IEpisodeMerger merger,
    IEpisodeRepository episodeRepo)
{
    public async Task UpdatePodcast(Podcast podcast, List<Episode> newEpisodes)
    {
        var existing = await episodeRepo.GetByPodcastId(podcast.Id).ToListAsync();
        var legacyExisting = existing.Select(ToLegacy).ToList();
        
        var result = await merger.MergeEpisodes(podcast, legacyExisting, newEpisodes);
        
        if (result.EpisodesToSave.Any())
        {
            await episodeRepo.Save(result.EpisodesToSave);
        }
    }
}
```

---

### 6. Podcast Updating Services

#### PodcastUpdaterV2
**Purpose:** Implements `IPodcastUpdater` with V2 repositories

**Methods:**
```csharp
Task<IndexPodcastResult> Update(
    Podcast podcast, 
    bool enrichOnly, 
    IndexingContext indexingContext);
```

**Features:**
- ✅ Fetches episodes from `IEpisodeRepository`
- ✅ Uses `IEpisodeMerger` for merge logic
- ✅ Enriches episodes via existing enrichers
- ✅ Filters episodes via elimination terms
- ✅ Saves podcast via `IPodcastRepositoryV2`
- ✅ Saves episodes via `IEpisodeRepository`

**Usage Example:**
```csharp
// Already used by:
// - AddAudioPodcastProcessor
// - EnrichYouTubePodcastProcessor (indirectly)
```

---

## Migration Path

### Step 1: Update Service Registration
Services are already registered! Just inject the V2 interface:

```csharp
// Old
public class MyService(IPodcastEpisodeProvider provider) { }

// New
public class MyService(IPodcastEpisodeProviderV2 provider) { }
```

### Step 2: Update Method Calls
V2 services typically accept `Guid podcastId` instead of `Podcast`:

```csharp
// Old
var episodes = provider.GetUntweetedPodcastEpisodes(podcast, days);

// New
var episodes = await provider.GetUntweetedPodcastEpisodes(podcast.Id);
```

### Step 3: Handle Async Operations
All V2 services use async/await:

```csharp
// Old (synchronous)
var episodes = filter.GetEpisodes(podcast, days);

// New (asynchronous)
var episodes = await filter.GetMostRecentUntweetedEpisodes(podcast.Id, days);
```

---

## Conversion Utilities

All V2 services include these helper methods:

### ToLegacyEpisode
Converts `Models.V2.Episode` → `Models.Episode`

```csharp
private static Episode ToLegacyEpisode(Models.V2.Episode v2Episode)
{
    return new Episode
    {
        Id = v2Episode.Id,
        Title = v2Episode.Title,
        // ... all properties mapped
    };
}
```

### ToLegacyPodcast
Converts `Models.V2.Podcast` → `Models.Podcast` (with episodes)

```csharp
private static Podcast ToLegacyPodcast(
    Models.V2.Podcast v2Podcast, 
    List<Episode> episodes)
{
    return new Podcast(v2Podcast.Id)
    {
        Name = v2Podcast.Name,
        Episodes = episodes,
        // ... all properties mapped
    };
}
```

### ToV2Episode
Converts `Models.Episode` → `Models.V2.Episode`

```csharp
private static Models.V2.Episode ToV2Episode(Podcast podcast, Episode episode)
{
    return new Models.V2.Episode
    {
        Id = episode.Id,
        PodcastId = podcast.Id,
        // ... all properties mapped
        PodcastName = podcast.Name,
        PodcastSearchTerms = podcast.SearchTerms,
    };
}
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task GetUntweetedEpisodes_ReturnsCorrectEpisodes()
{
    // Arrange
    var mockPodcastRepo = new Mock<IPodcastRepositoryV2>();
    var mockEpisodeRepo = new Mock<IEpisodeRepository>();
    var filter = new PodcastEpisodeFilterV2(
        mockPodcastRepo.Object,
        mockEpisodeRepo.Object,
        options,
        logger);
    
    // Setup mocks...
    
    // Act
    var result = await filter.GetMostRecentUntweetedEpisodes(podcastId, 7);
    
    // Assert
    Assert.NotEmpty(result);
}
```

### Integration Tests
```csharp
[Fact]
public async Task EndToEnd_PostEpisode_UpdatesRepository()
{
    // Use real repositories against test database
    var provider = new PodcastEpisodeProviderV2(/* real deps */);
    var poster = new PodcastEpisodePosterV2(/* real deps */);
    
    var episodes = await provider.GetUntweetedPodcastEpisodes(podcastId);
    var result = await poster.PostPodcastEpisode(episodes.First());
    
    // Verify episode marked as posted in database
    var updated = await episodeRepo.GetEpisode(podcastId, episodeId);
    Assert.True(updated.Posted);
}
```

---

## Performance Considerations

### Episode Loading
V2 services load episodes from Cosmos DB per-podcast:

```csharp
// Efficient: Single query per podcast
var episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

// Uses partition key /podcastId for fast queries
```

### Batch Operations
When processing multiple podcasts:

```csharp
// Good: Process podcasts in parallel
var tasks = podcastIds.Select(async id =>
{
    var episodes = await filter.GetMostRecentUntweetedEpisodes(id, days);
    return episodes;
});
var results = await Task.WhenAll(tasks);
```

### Caching Considerations
- Podcast metadata can be cached (changes infrequently)
- Episode lists should not be cached (change frequently)
- Use `IDistributedCache` for podcast metadata if needed

---

## Troubleshooting

### Common Issues

**Issue:** "Podcast not found"
```csharp
// Solution: Ensure podcast exists in IPodcastRepositoryV2
var podcast = await podcastRepo.GetBy(x => x.Id == podcastId);
if (podcast == null)
{
    logger.LogWarning("Podcast {PodcastId} not found", podcastId);
    return Enumerable.Empty<PodcastEpisode>();
}
```

**Issue:** "Episodes not loaded"
```csharp
// Solution: Check partition key and episode container
var episodes = await episodeRepo.GetByPodcastId(podcastId).ToListAsync();
logger.LogInformation("Loaded {Count} episodes for {PodcastId}", 
    episodes.Count, podcastId);
```

**Issue:** "Bundled episodes not found"
```csharp
// Solution: Verify TitleRegex is set and episodes exist
if (string.IsNullOrWhiteSpace(podcast.TitleRegex))
{
    logger.LogWarning("Podcast {PodcastId} has Bundles=true but no TitleRegex", 
        podcastId);
}
```

---

## Next Steps

### Ready to Migrate
These consumers can now use V2 services:
- ✅ `Console-Apps/Tweet/TweetProcessor.cs`
- ✅ `Console-Apps/Poster/PostProcessor.cs`
- ✅ API handlers using `IPodcastEpisodeProvider`

### Still Needed
- URL submission services V2
- API handler migrations
- Comprehensive test suite

---

Last Updated: Current session
