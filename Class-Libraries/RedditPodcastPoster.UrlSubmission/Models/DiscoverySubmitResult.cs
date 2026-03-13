using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Episode? Episode = null
);