using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Episode? Episode = null
);