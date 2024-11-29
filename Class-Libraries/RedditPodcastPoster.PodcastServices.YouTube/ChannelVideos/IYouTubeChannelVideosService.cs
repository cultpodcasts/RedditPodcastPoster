using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;

public interface IYouTubeChannelVideosService : IFlushable
{
    Task<Models.ChannelVideos?> GetChannelVideos(
        YouTubeChannelId channelId,
        IndexingContext indexingContext);
}