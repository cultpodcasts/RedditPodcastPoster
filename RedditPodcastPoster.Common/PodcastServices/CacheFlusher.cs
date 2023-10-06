using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;

namespace RedditPodcastPoster.Common.PodcastServices;

public class CacheFlusher : IFlushable
{
    private readonly ICachedApplePodcastService _cachedApplePodcastService;
    private readonly ILogger<CacheFlusher> _logger;

    public CacheFlusher(
        ICachedApplePodcastService cachedApplePodcastService,
        ILogger<CacheFlusher> logger)
    {
        _cachedApplePodcastService = cachedApplePodcastService;
        _logger = logger;
    }

    public void Flush()
    {
        _cachedApplePodcastService.Flush();
    }
}