using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryService
{
    IAsyncEnumerable<DiscoveryResult> GetDiscoveryResults(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext);
}