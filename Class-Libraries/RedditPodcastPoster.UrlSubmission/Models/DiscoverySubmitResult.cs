namespace RedditPodcastPoster.UrlSubmission.Models;

public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Guid? EpisodeId = null
);