namespace RedditPodcastPoster.UrlSubmission.Models;

public enum SubmitResultState
{
    None = 0,
    Created,
    Enriched,
    PodcastRemoved,
    EpisodeAlreadyExists
}