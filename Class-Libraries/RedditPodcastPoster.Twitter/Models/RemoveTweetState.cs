namespace RedditPodcastPoster.Twitter.Models;

public enum RemoveTweetState
{
    Unknown = 0,
    TooManyRequests,
    Deleted,
    Other,
    NotFound
}