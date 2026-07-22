using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

public class CachedApplePodcastService(
    IApplePodcastService applePodcastService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CachedApplePodcastService> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICachedApplePodcastService, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<string, IEnumerable<AppleEpisode>?> _episodesCache = new();

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (!_episodesCache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes = _episodesCache[cacheKey] =
                await applePodcastService.GetEpisodes(podcastId, indexingContext);
        }

        return podcastEpisodes;
    }

    public async Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (_episodesCache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            return podcastEpisodes?.SingleOrDefault(x => x.Id == episodeId);
        }

        return await applePodcastService.GetEpisode(episodeId, indexingContext);
    }

    public async Task<AppleEpisode?> SingleUseGetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (!_episodesCache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes = await GetEpisodes(podcastId, indexingContext);
        }

        return podcastEpisodes?.SingleOrDefault(x => x.Id == episodeId);
    }

    public void ClearPassCache()
    {
        _episodesCache.Clear();
    }

    private string GetCacheKey(long podcastId, DateTime? releasedSince)
    {
        if (releasedSince.HasValue)
        {
            return $"{podcastId}-{releasedSince.Value.Ticks}";
        }

        return $"{podcastId}";
    }
}
