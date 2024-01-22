using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.PodcastServices;

public class CacheFlusher(
    ICachedApplePodcastService cachedApplePodcastService,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelService youTubeChannelService,
    ILogger<CacheFlusher> logger)
    : IFlushable
{
    public void Flush()
    {
        cachedApplePodcastService.Flush();
        youTubeChannelVideoSnippetsService.Flush();
        youTubeChannelService.Flush();
    }
}