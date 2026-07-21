using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface IDiscoveryPublisher
{
    Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo);
}
