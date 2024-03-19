namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EpisodeResult(
    string Id,
    DateTime Released,
    string Description,
    string EpisodeName,
    TimeSpan? Length,
    string ShowName,
    DiscoveryService DiscoveryService,
    Uri? Url = null,
    string? ServicePodcastId = null);