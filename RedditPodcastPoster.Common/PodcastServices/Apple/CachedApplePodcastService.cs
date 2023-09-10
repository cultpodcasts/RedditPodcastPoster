using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class CachedApplePodcastService : ICachedApplePodcastService
{
    private static readonly ConcurrentDictionary<long, IEnumerable<AppleEpisode>?> Cache = new();
    private readonly IApplePodcastService _applePodcastService;
    private readonly ILogger<CachedApplePodcastService> _logger;

    public CachedApplePodcastService(
        IApplePodcastService applePodcastService,
        ILogger<CachedApplePodcastService> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<IEnumerable<AppleEpisode>> GetEpisodes(long podcastId)
    {
        if (!Cache.TryGetValue(podcastId, out var podcastEpisodes))
        {
            podcastEpisodes= Cache[podcastId] = await _applePodcastService.GetEpisodes(podcastId);
        }

        return podcastEpisodes!;
    }
}