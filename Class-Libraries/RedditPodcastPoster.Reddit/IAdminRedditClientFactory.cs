namespace RedditPodcastPoster.Reddit;

public interface IAdminRedditClientFactory
{
    IAdminRedditClient Create();
}