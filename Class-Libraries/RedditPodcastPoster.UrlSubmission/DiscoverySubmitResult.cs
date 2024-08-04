namespace RedditPodcastPoster.UrlSubmission;

public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Guid? EpisodeId = null
);