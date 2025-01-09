namespace RedditPodcastPoster.Twitter.Models;

public enum RemoveTweetsState
{
    Unknown = 0,
    TooManyRequests,
    Deleted,
    Other,
    NotFound
}