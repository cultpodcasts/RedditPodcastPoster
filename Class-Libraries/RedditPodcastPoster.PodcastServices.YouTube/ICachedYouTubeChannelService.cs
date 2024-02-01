using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface ICachedYouTubeChannelService : IYouTubeChannelService, IFlushable
{
}