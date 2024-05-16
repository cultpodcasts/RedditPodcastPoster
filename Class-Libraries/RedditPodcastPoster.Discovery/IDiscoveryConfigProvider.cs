namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryServiceConfigProvider
{
    IEnumerable<DiscoveryConfig.ServiceConfig> GetServiceConfigs(
        bool excludeSpotify, 
        bool includeYouTube,
        bool includeListenNotes);
}