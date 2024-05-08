namespace RedditPodcastPoster.UrlSubmission;

public record SubmitOptions(
    Guid? PodcastId,
    bool MatchOtherServices,
    bool PersistToDatabase = true);