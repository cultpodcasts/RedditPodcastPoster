using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.PodcastServices;

public class CacheFlusher : IFlushable
{
    private readonly ICachedSpotifyClient _cachedSpotifyClient;
    private readonly ICachedApplePodcastService _cachedApplePodcastService;
    private readonly ILogger<CacheFlusher> _logger;

    public CacheFlusher(
        ICachedSpotifyClient cachedSpotifyClient,
        ICachedApplePodcastService cachedApplePodcastService,   
        ILogger<CacheFlusher> logger)
    {
        _cachedSpotifyClient = cachedSpotifyClient;
        _cachedApplePodcastService = cachedApplePodcastService;
        _logger = logger;
    }

    public void Flush()
    {
        _logger.LogInformation($"{nameof(Flush)}");
        _cachedApplePodcastService.Flush();
        _cachedSpotifyClient.Flush();
    }
}