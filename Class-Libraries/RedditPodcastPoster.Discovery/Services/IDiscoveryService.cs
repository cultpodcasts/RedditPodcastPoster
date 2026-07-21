using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Services;

public interface IDiscoveryService
{
    IAsyncEnumerable<DiscoveryResult> GetDiscoveryResults(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext);
}
