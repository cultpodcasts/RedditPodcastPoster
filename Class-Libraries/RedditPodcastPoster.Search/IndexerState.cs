namespace RedditPodcastPoster.Search;

public enum IndexerState
{
    Unknown = 0,
    Executed,
    Failure,
    TooManyRequests,
    AlreadyRunning
}