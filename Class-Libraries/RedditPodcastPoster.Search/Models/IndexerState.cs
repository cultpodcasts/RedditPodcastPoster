namespace RedditPodcastPoster.Search.Models;

public enum IndexerState
{
    Unknown = 0,
    Executed,
    Failure,
    TooManyRequests,
    AlreadyRunning
}