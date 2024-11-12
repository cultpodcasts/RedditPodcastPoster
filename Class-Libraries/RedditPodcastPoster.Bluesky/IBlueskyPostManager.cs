namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPostManager
{
    Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed);
}