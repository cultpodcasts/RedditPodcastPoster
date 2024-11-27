using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

namespace RedditPodcastPoster.PodcastServices;

public class CacheFlusher(
    ICachedApplePodcastService cachedApplePodcastService,
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
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
        youTubeChannelVideosService.Flush();
        spotifyPodcastEpisodesProvider.Flush();
    }
}