using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeChannelVideosService : IFlushable
{
    Task<ChannelVideos?> GetChannelVideos(
        YouTubeChannelId channelId,
        IndexingContext indexingContext);
}