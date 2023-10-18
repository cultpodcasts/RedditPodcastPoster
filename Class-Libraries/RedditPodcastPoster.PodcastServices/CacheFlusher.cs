using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.PodcastServices;

public class CacheFlusher : IFlushable
{
    private readonly ICachedApplePodcastService _cachedApplePodcastService;
    private readonly IYouTubeChannelVideoSnippetsService _youTubeChannelVideoSnippetsService;
    private readonly IYouTubeChannelService _youTubeChannelService;
    private readonly ILogger<CacheFlusher> _logger;

    public CacheFlusher(
        ICachedApplePodcastService cachedApplePodcastService,
        IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
        IYouTubeChannelService youTubeChannelService,
        ILogger<CacheFlusher> logger)
    {
        _cachedApplePodcastService = cachedApplePodcastService;
        _youTubeChannelVideoSnippetsService = youTubeChannelVideoSnippetsService;
        _youTubeChannelService = youTubeChannelService;
        _logger = logger;
    }

    public void Flush()
    {
        _cachedApplePodcastService.Flush();
        _youTubeChannelVideoSnippetsService.Flush();
        _youTubeChannelService.Flush();
    }
}