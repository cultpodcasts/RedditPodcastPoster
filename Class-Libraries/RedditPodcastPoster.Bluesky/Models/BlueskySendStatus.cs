namespace RedditPodcastPoster.Bluesky.Models;

public enum BlueskySendStatus
{
    Unknown = 0,
    Success,
    Failure,
    FailureHttp,
    FailureAuth
}