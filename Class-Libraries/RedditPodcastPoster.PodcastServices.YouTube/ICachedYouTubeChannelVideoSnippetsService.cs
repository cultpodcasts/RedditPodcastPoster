using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface ICachedYouTubeChannelVideoSnippetsService : IYouTubeChannelVideoSnippetsService, IFlushable
{
}