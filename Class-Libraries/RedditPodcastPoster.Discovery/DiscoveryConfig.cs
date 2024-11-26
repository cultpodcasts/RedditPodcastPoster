namespace RedditPodcastPoster.Discovery;

public record DiscoveryConfig(
    DateTime Since,
    TimeSpan? TaddyOffset,
    IEnumerable<ServiceConfig> ServiceConfigs,
    bool EnrichFromSpotify,
    bool EnrichFromApple);