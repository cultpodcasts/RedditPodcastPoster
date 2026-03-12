using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IDiscoveryPublisher
{
    Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo);
}
