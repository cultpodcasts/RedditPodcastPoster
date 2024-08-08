namespace RedditPodcastPoster.Twitter;

public enum TweetSendStatus
{
    Sent = 1,
    Failed,
    DuplicateForbidden,
    TooManyRequests
}