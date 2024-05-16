namespace RedditPodcastPoster.Discovery;

public record DiscoveryConfig(IEnumerable<ServiceConfig> ServiceConfigs, bool EnrichFromSpotify)
{
}