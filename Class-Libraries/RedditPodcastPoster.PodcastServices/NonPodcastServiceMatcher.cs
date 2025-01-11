using RedditPodcastPoster.BBC;

namespace RedditPodcastPoster.PodcastServices;

public static class NonPodcastServiceMatcher
{
    public static bool MatchesInternetArchive(Uri url)
    {
        return url.Host.ToLower().Contains("archive.org") && url.AbsolutePath.StartsWith("/details");
    }

    public static bool MatchesBBC(Uri url)
    {
        return ServiceMatcher.IsIplayer(url) || ServiceMatcher.IsSounds(url);
    }
}