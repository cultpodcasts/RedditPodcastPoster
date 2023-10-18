namespace RedditPodcastPoster.Twitter;

public interface ITwitterClient
{
    Task<bool> Send(string tweet);
}