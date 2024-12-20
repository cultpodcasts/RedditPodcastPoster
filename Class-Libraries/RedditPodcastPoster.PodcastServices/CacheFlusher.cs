using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices;

public class CacheFlusher(
    ICachedApplePodcastService cachedApplePodcastService,
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    ICachedTolerantYouTubePlaylistService cachedTolerantYouTubePlaylist,
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
        cachedTolerantYouTubePlaylist.Flush();
        spotifyPodcastEpisodesProvider.Flush();
    }
}