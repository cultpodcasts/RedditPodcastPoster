# IPodcastRepository vs IPodcastRepositoryV2

## Overview

Two podcast repository interfaces exist during the migration:

| Aspect | IPodcastRepository (Legacy) | IPodcastRepositoryV2 (V2) |
|--------|----------------------------|---------------------------|
| **Model** | `Models.Podcast` (with embedded episodes) | `Models.V2.Podcast` (no episodes) |
| **Container** | `CultPodcasts` | `Podcasts` |
| **Episodes** | Embedded in document | Detached in Episodes container |
| **Methods** | 17 methods including projections | 5 core methods (simplified) |
| **Merge** | `Merge()` mutates episodes | N/A (use IEpisodeMerger) |
| **Queries** | Episode-based queries (GetPodcastIdsWithUntweetedReleasedSince) | Podcast metadata only |

---

## Key Differences

### 1. Episodes Handling

**Legacy:**
```csharp
var podcast = await podcastRepository.GetPodcast(id);
var episodes = podcast.Episodes; // Embedded
```

**V2:**
```csharp
var podcast = await podcastRepositoryV2.GetPodcast(id);
var episodes = await episodeRepository.GetByPodcastId(id).ToListAsync();
// Episodes loaded separately
```

---

### 2. Episode Queries

**Legacy:**
```csharp
// Repository knows about episodes
var podcastIds = await podcastRepository.GetPodcastIdsWithUntweetedReleasedSince(since);
// Queries embedded episodes
```

**V2:**
```csharp
// Use IEpisodeRepository for episode queries
var untweetedEpisodes = await episodeRepository.GetAllBy(x => 
    x.Release >= since && 
    !x.Tweeted &&
    !x.Removed
).ToListAsync();

var podcastIds = untweetedEpisodes.Select(e => e.PodcastId).Distinct();
```

---

### 3. Merge Operations

**Legacy:**
```csharp
var result = podcastRepository.Merge(podcast, newEpisodes);
// Mutates podcast.Episodes
await podcastRepository.Save(podcast);
// Saves embedded episodes
```

**V2:**
```csharp
var existingEpisodes = await episodeRepository.GetByPodcastId(id).ToListAsync();
var result = await episodeMerger.MergeEpisodes(podcast, existing, newEpisodes);
// Returns V2 episodes to save
await episodeRepository.Save(result.EpisodesToSave);
// Saves detached episodes
```

---

### 4. Projections

**Legacy:**
```csharp
// Supports projections
var podcastIdOnly = await repository.GetBy(
    x => x.Name == name, 
    x => new { x.Id });
```

**V2:**
```csharp
// No projections - get full object
var podcast = await repository.GetBy(x => x.Name == name);
var id = podcast?.Id;
// Simplified API
```

---

### 5. Method Count

**Legacy: 17 methods**
- GetPodcast, Save, Merge
- GetAll, GetAllIds, GetTotalCount
- GetBy (with/without projection)
- GetAllBy (with/without projection)
- Episode-based queries (5 methods)
- GetAllFileKeys
- PodcastHasEpisodesAwaitingEnrichment

**V2: 5 methods**
- GetPodcast
- Save
- GetAll
- GetBy
- GetAllBy

**Rationale:** V2 focuses on podcast metadata only. Episode operations go through `IEpisodeRepository`.

---

## Migration Strategy

### Current State: Dual Repositories
Both repositories are registered and functional:

```csharp
services
    .AddScoped<IPodcastRepository, PodcastRepository>()      // Legacy
    .AddScoped<IPodcastRepositoryV2, PodcastRepositoryV2>(); // V2
```

### When to Use Each

**Use IPodcastRepository (Legacy) when:**
- ❌ Working with legacy code not yet migrated
- ❌ Need embedded episodes
- ❌ Using legacy services

**Use IPodcastRepositoryV2 (V2) when:**
- ✅ Writing new code
- ✅ Working with detached episodes
- ✅ Using V2 services
- ✅ **Always prefer V2 for new work**

---

## Container Mapping

### Legacy
```
IPodcastRepository → CultPodcasts container
- Document structure: { id, name, episodes: [...] }
- Episodes embedded in podcast document
```

### V2
```
IPodcastRepositoryV2 → Podcasts container
- Document structure: { id, name, ... }
- No episodes property

IEpisodeRepository → Episodes container
- Document structure: { id, podcastId, title, ... }
- Partition key: /podcastId
```

---

## Performance Comparison

### Query: Get podcast with episodes

**Legacy (1 query):**
```csharp
var podcast = await podcastRepository.GetPodcast(id);
// All episodes loaded automatically
// RU cost: Higher (large document)
```

**V2 (2 queries):**
```csharp
var podcast = await podcastRepositoryV2.GetPodcast(id);
var episodes = await episodeRepository.GetByPodcastId(id).ToListAsync();
// Two smaller queries, partition-optimized
// RU cost: Lower (optimized)
```

### Query: Get podcasts with untweeted episodes

**Legacy:**
```csharp
var podcastIds = await repository.GetPodcastIdsWithUntweetedReleasedSince(since);
// Scans embedded episodes across all podcasts
// RU cost: Very high (cross-partition query)
```

**V2:**
```csharp
var episodes = await episodeRepository.GetAllBy(x => 
    x.Release >= since && !x.Tweeted).ToListAsync();
var podcastIds = episodes.Select(e => e.PodcastId).Distinct();
// Direct episode query (faster, lower RU)
```

---

## Future Plan

### Phase 1: Coexistence (Current)
- ✅ Both repositories functional
- ✅ Services choose which to use
- ✅ Gradual migration

### Phase 2: Deprecation
- Mark `IPodcastRepository` as `[Obsolete]`
- Migrate all remaining consumers
- Monitor usage

### Phase 3: Removal
- Remove `IPodcastRepository`
- Remove `PodcastRepository` implementation
- Rename `IPodcastRepositoryV2` → `IPodcastRepository`
- Remove V2 suffix (becomes default)

---

## Quick Reference

### ✅ Currently Using IPodcastRepositoryV2
- PodcastUpdaterV2
- All V2 services
- SubmitUrlHandler
- EpisodeHandler
- PodcastHandler
- Most console processors

### ❌ Still Using IPodcastRepository (Legacy)
- PodcastService (query-only, used by categoriser)
- Legacy service implementations
- Some internal tooling

**Status:** Most production code already using V2! 🎉

---

**TL;DR:**
- **IPodcastRepository** = Legacy (embedded episodes, CultPodcasts container)
- **IPodcastRepositoryV2** = V2 (no episodes, Podcasts container)
- **When in doubt: Use V2** ✅
