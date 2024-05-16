using RedditPodcastPoster.PodcastServices.Abstractions;
using static RedditPodcastPoster.Discovery.DiscoveryConfig;

namespace RedditPodcastPoster.Discovery;

public record DiscoveryConfig(IEnumerable<ServiceConfig> ServiceConfigs, bool EnrichFromSpotify)
{
    public record ServiceConfig(string Term, DiscoverService DiscoverService);
}