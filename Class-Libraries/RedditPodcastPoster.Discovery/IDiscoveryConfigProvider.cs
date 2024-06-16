namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryServiceConfigProvider
{
    IEnumerable<ServiceConfig> GetServiceConfigs(GetServiceConfigOptions options);
}