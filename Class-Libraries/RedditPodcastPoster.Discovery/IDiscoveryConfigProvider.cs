namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryServiceConfigProvider
{
    IEnumerable<ServiceConfig> GetServiceConfigs(
        bool excludeSpotify,
        bool includeYouTube,
        bool includeListenNotes);
}