namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryServiceConfigProvider
{
    DiscoveryConfig CreateDiscoveryConfig(GetServiceConfigOptions options);
}