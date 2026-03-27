using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Episode? Episode = null
);