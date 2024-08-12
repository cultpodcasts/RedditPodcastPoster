namespace RedditPodcastPoster.Twitter;

public interface ITweeter
{
    Task Tweet(
        bool youTubeRefreshed, 
        bool spotifyRefreshed);
}