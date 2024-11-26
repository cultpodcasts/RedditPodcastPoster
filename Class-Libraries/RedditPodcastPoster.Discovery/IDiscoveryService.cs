using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryService
{
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResults(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext);
}