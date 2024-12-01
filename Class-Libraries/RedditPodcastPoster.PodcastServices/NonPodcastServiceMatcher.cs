namespace RedditPodcastPoster.PodcastServices;

public static class NonPodcastServiceMatcher
{
    public static bool IsMatch(Uri url)
    {
        return
            (url.Host.ToLower().Contains("archive.org") && url.AbsolutePath.StartsWith("/details")) ||
            (url.Host.ToLower().Contains("bbc.co.uk") && url.AbsolutePath.StartsWith("/iplayer/episode"));
    }
}