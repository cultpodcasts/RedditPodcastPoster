namespace RedditPodcastPoster.UrlSubmission;

public enum SubmitResultState
{
    None = 0,
    Created,
    Enriched,
    PodcastRemoved,
    EpisodeAlreadyExists
}