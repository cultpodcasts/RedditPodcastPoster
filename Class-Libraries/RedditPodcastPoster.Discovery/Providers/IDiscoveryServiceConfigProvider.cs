using RedditPodcastPoster.Discovery.Models;

namespace RedditPodcastPoster.Discovery.Providers;

public interface IDiscoveryServiceConfigProvider
{
    DiscoveryConfig CreateDiscoveryConfig(GetServiceConfigOptions options);
}