namespace RedditPodcastPoster.Discovery.Models;

public record DiscoveryConfig(
    DateTime Since,
    TimeSpan? TaddyOffset,
    IEnumerable<ServiceConfig> ServiceConfigs,
    bool EnrichFromSpotify,
    bool EnrichFromApple);