namespace RedditPodcastPoster.BBC.Matching;

public static class ServiceMatcher
{
    public static bool IsIplayer(Uri url) => BBCUrlMatcher.IsIplayerEpisodeUrl(url);

    public static bool IsSounds(Uri url) => BBCUrlMatcher.IsSoundsPlayUrl(url);
}
