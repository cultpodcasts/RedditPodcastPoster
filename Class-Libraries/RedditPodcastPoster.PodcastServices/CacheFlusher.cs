using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.PodcastServices;

public class CacheFlusher(
    ICachedApplePodcastService cachedApplePodcastService,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelService youTubeChannelService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CacheFlusher> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IFlushable
{
    public void Flush()
    {
        cachedApplePodcastService.Flush();
        youTubeChannelVideoSnippetsService.Flush();
        youTubeChannelService.Flush();
    }
}