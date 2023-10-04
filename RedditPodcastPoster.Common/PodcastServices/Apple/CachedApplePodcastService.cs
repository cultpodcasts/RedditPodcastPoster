using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class CachedApplePodcastService : ICachedApplePodcastService
{
    private static readonly ConcurrentDictionary<string, IEnumerable<AppleEpisode>?> Cache = new();
    private readonly IApplePodcastService _applePodcastService;
    private readonly ILogger<CachedApplePodcastService> _logger;

    public CachedApplePodcastService(
        IApplePodcastService applePodcastService,
        ILogger<CachedApplePodcastService> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (!Cache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes = Cache[cacheKey] = await _applePodcastService.GetEpisodes(podcastId, indexingContext);
        }

        return podcastEpisodes;
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