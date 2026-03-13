# PodcastEpisodeV2 - V2 Model Pairing

## Overview
`PodcastEpisodeV2` is a record type that pairs a `Models.V2.Podcast` with a `Models.V2.Episode`, providing a V2-native alternative to the legacy `PodcastEpisode` type.

## Purpose

**Problem Solved:**
- V2 services were loading V2 models, converting to legacy, then wrapping in legacy `PodcastEpisode`
- Unnecessary conversion overhead
- Mixing of V2 and legacy models

**Solution:**
- `PodcastEpisodeV2` pairs V2 models directly
- V2 services can work with V2 models end-to-end
- Conversion only happens at compatibility boundaries

## Definition

```csharp
// Legacy
public record PodcastEpisode(Podcast Podcast, Episode Episode);

// V2
public record PodcastEpisodeV2(Models.V2.Podcast Podcast, Models.V2.Episode Episode);
```

## Usage Examples

### Loading from V2 Repositories

```csharp
public class MyServiceV2(
    IPodcastRepositoryV2 podcastRepo,
    IEpisodeRepository episodeRepo)
{
    public async Task<IEnumerable<PodcastEpisodeV2>> GetEpisodes(Guid podcastId)
    {
        // Load V2 models
        var v2Podcast = await podcastRepo.GetBy(x => x.Id == podcastId);
        var v2Episodes = await episodeRepo.GetByPodcastId(podcastId).ToListAsync();
        
        // Create V2 pairs directly - no conversion!
        return v2Episodes.Select(e => new PodcastEpisodeV2(v2Podcast, e));
    }
}
```

### V2 Services with V2-Native Methods

```csharp
// V2-native method (preferred)
var v2Episodes = await filter.GetMostRecentUntweetedEpisodesV2(podcastId, 7);
// Returns IEnumerable<PodcastEpisodeV2>

// Legacy compatibility method
var legacyEpisodes = await filter.GetMostRecentUntweetedEpisodes(podcastId, 7);
// Returns IEnumerable<PodcastEpisode> (converted internally)
```

## Conversion Extensions

The `PodcastEpisodeExtensions` class provides seamless conversion:

### V2 → Legacy

```csharp
using RedditPodcastPoster.Models.Extensions;

PodcastEpisodeV2 v2Episode = /* ... */;

// Convert to legacy
PodcastEpisode legacyEpisode = v2Episode.ToLegacy();

// Or convert just the episode
Episode legacyEp = v2Episode.Episode.ToLegacyEpisode();
```

### Legacy → V2

```csharp
PodcastEpisode legacyEpisode = /* ... */;

// Convert to V2 (should be rare - prefer loading from V2 repos)
PodcastEpisodeV2 v2Episode = legacyEpisode.ToV2();
```

## V2 Service Pattern

V2 services now offer both V2-native and legacy-compatible methods:

```csharp
public interface IPodcastEpisodeFilterV2
{
    // V2-native methods (preferred for new code)
    Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodesV2(
        Guid podcastId, int numberOfDays);
    
    // Legacy compatibility methods
    Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Guid podcastId, int numberOfDays);
}
```

**Implementation Pattern:**
```csharp
public class PodcastEpisodeFilterV2 : IPodcastEpisodeFilterV2
{
    // V2-native implementation (no conversions)
    public async Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodesV2(
        Guid podcastId, int numberOfDays)
    {
        var v2Podcast = await podcastRepo.GetBy(x => x.Id == podcastId);
        var v2Episodes = await episodeRepo.GetByPodcastId(podcastId).ToListAsync();
        
        return v2Episodes
            .Where(/* filtering */)
            .Select(e => new PodcastEpisodeV2(v2Podcast, e));
    }
    
    // Legacy wrapper (converts at boundary)
    public async Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Guid podcastId, int numberOfDays)
    {
        var v2Results = await GetMostRecentUntweetedEpisodesV2(podcastId, numberOfDays);
        return v2Results.Select(x => x.ToLegacy());
    }
}
```

## Benefits

### 1. Performance
- **Before:** V2 → Legacy → PodcastEpisode → (maybe back to V2)
- **After:** V2 → PodcastEpisodeV2 (direct, no conversion)

### 2. Type Safety
- V2 services work with V2 models end-to-end
- Explicit conversion points at boundaries
- Clearer separation of concerns

### 3. Migration Path
- New code uses V2-native methods (`*V2` suffix)
- Legacy code uses compatibility methods
- Gradual migration consumer-by-consumer

### 4. Future-Proof
- When legacy models removed, just:
  1. Remove legacy methods
  2. Remove `V2` suffix from method names
  3. Done!

## Migration Examples

### Before (Legacy Service)
```csharp
public class TweetService(IPodcastEpisodeProvider provider)
{
    public async Task Tweet()
    {
        var podcast = await podcastRepo.GetPodcast(podcastId);
        var episodes = provider.GetMostRecentUntweetedEpisodes(podcast, 7);
        // podcast.Episodes collection used internally
    }
}
```

### After (V2 Service - Legacy Compatible)
```csharp
public class TweetService(IPodcastEpisodeProviderV2 provider)
{
    public async Task Tweet()
    {
        // Uses legacy method for compatibility
        var episodes = await provider.GetMostRecentUntweetedEpisodes(podcastId);
        // Still works with PodcastEpisode
    }
}
```

### After (V2 Service - V2 Native)
```csharp
public class TweetServiceV2(IPodcastEpisodeProviderV2 provider)
{
    public async Task Tweet()
    {
        // Uses V2-native method
        var v2Episodes = await provider.GetMostRecentUntweetedEpisodesV2(podcastId);
        // Works with PodcastEpisodeV2 - no conversions!
        
        foreach (var v2Episode in v2Episodes)
        {
            // Process v2Episode.Podcast and v2Episode.Episode directly
        }
    }
}
```

## Current V2 Services Supporting PodcastEpisodeV2

✅ **IPodcastEpisodeFilterV2** - Fully supports both V2-native and legacy methods
- `GetNewEpisodesReleasedSinceV2()` → `PodcastEpisodeV2`
- `GetMostRecentUntweetedEpisodesV2()` → `PodcastEpisodeV2`
- `GetMostRecentBlueskyReadyEpisodesV2()` → `PodcastEpisodeV2`

🔄 **Next to Add:**
- `IPodcastEpisodeProviderV2` - Add `*V2()` methods
- `IPodcastEpisodePosterV2` - Accept `PodcastEpisodeV2`

## Best Practices

### Do ✅
- Use `*V2()` methods in new code
- Load V2 models from repositories directly
- Create `PodcastEpisodeV2` pairs without conversion
- Convert to legacy only at boundaries (UI, legacy APIs)

### Don't ❌
- Convert V2 → Legacy → V2 unnecessarily
- Use legacy methods in new V2-native code
- Mix V2 and legacy models in same service
- Load legacy then convert to V2 (load V2 directly instead)

## Files

**Models:**
- `Class-Libraries\RedditPodcastPoster.Models\PodcastEpisodeV2.cs`
- `Class-Libraries\RedditPodcastPoster.Models\Extensions\PodcastEpisodeExtensions.cs`

**V2 Services:**
- `Class-Libraries\RedditPodcastPoster.Common\Episodes\IPodcastEpisodeFilterV2.cs`
- `Class-Libraries\RedditPodcastPoster.Common\Episodes\PodcastEpisodeFilterV2.cs`

---

Last Updated: Current session
Status: ✅ Implemented and tested
