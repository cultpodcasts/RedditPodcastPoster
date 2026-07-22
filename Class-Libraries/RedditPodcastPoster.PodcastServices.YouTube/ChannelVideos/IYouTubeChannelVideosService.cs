using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;

public interface IYouTubeChannelVideosService
{
    Task<Models.ChannelVideos?> GetChannelVideos(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool expensivePlaylist = false);
}
