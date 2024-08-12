namespace RedditPodcastPoster.Twitter;

public interface ITweeter
{
    Task Tweet(
        bool youTubeRefreshed=true, 
        bool spotifyRefreshed=true);
}