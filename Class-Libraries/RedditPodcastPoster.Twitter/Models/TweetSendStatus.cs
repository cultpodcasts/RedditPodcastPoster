namespace RedditPodcastPoster.Twitter.Models;

public enum TweetSendStatus
{
    Sent = 1,
    Failed,
    DuplicateForbidden,
    TooManyRequests
}
