namespace RedditPodcastPoster.Indexing.Models;

public enum IndexStatus
{
    Unset = 0,
    NotFound,
    NotPerformed,
    Performed,

    /// <summary>Another on-demand or hourly index already holds this podcast's indexing lease.</summary>
    AlreadyRunning,

    /// <summary>The on-demand time budget cut the index off (HTTP 504); what was saved is not known at the cut.</summary>
    TimedOut
}