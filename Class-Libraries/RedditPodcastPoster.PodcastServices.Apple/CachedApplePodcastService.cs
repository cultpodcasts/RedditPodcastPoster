using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class CachedApplePodcastService(
    IApplePodcastService applePodcastService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CachedApplePodcastService> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICachedApplePodcastService
{
    private static readonly ConcurrentDictionary<string, IEnumerable<AppleEpisode>?> Cache = new();

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (!Cache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes = Cache[cacheKey] = await applePodcastService.GetEpisodes(podcastId, indexingContext);
        }

        return podcastEpisodes;
    }

    public void Flush()
    {
        Cache.Clear();
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