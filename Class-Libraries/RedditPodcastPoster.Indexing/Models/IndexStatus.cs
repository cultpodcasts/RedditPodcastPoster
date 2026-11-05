namespace RedditPodcastPoster.Indexing.Models;

public enum IndexStatus
{
    Unset = 0,
    NotFound,
    NotPerformed,
    Performed,

    /// <summary>Another on-demand or hourly index already holds this podcast's indexing lease.</summary>
    AlreadyRunning
}