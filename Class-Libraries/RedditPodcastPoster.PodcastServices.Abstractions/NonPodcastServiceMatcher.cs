namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class NonPodcastServiceMatcher
{
    public static bool MatchesInternetArchive(Uri url)
    {
        return url.Host.ToLower().Contains("archive.org") && url.AbsolutePath.StartsWith("/details");
    }

    public static bool MatchesBBC(Uri url)
    {
        var host = url.Host.ToLower();
        return host.Contains("bbc.co.uk") &&
               (url.AbsolutePath.StartsWith("/iplayer/episode") ||
                url.AbsolutePath.StartsWith("/sounds/play/"));
    }
}
