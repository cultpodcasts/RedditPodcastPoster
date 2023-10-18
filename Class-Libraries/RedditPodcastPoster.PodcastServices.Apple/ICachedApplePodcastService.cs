using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface ICachedApplePodcastService : IApplePodcastService, IFlushable
{
}