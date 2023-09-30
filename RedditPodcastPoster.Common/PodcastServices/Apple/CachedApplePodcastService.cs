﻿using System.Collections.Concurrent;
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

    public async Task<IEnumerable<AppleEpisode>> GetEpisodes(long podcastId, DateTime releasedSince)
    {
        var cacheKey = GetCacheKey(podcastId, releasedSince);
        if (!Cache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes= Cache[cacheKey] = await _applePodcastService.GetEpisodes(podcastId, releasedSince);
        }

        return podcastEpisodes!;
    }

    private string GetCacheKey(long podcastId, DateTime releasedSince)
    {
        return $"{podcastId}-{releasedSince.Ticks}";
    }
}