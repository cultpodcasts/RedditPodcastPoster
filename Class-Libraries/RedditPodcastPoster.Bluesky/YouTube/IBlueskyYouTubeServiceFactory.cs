namespace RedditPodcastPoster.Bluesky.YouTube;

public interface IBlueskyYouTubeServiceFactory
{
    IBlueskyYouTubeServiceWrapper Create();
}