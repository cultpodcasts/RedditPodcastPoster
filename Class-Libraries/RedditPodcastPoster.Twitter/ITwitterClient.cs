namespace RedditPodcastPoster.Twitter;

public interface ITwitterClient
{
    Task<TweetSendStatus> Send(string tweet);
}