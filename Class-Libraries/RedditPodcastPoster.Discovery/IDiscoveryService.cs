using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryService
{
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResults(
        IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig);
}