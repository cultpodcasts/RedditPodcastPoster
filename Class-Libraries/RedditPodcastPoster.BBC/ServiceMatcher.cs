namespace RedditPodcastPoster.BBC;

public static class ServiceMatcher
{
    private static bool IsBBC(Uri url)
    {
        return url.Host.ToLower().Contains("bbc.co.uk");
    }
    public static bool IsIplayer(Uri url)
    {
        return IsBBC(url) && url.AbsolutePath.StartsWith("/iplayer/episode");
    }
    public static bool IsSounds(Uri url)
    {
        return IsBBC(url) && url.AbsolutePath.StartsWith("/sounds/play/");
    }

}
