using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryService
{
    IAsyncEnumerable<DiscoveryResult> GetDiscoveryResults(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext);
}
