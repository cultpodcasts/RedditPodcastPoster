namespace RedditPodcastPoster.InternetArchive.Matching;

public static class InternetArchiveUrlMatcher
{
    public static bool IsInternetArchiveUrl(Uri url)
    {
        return url.Host.Contains("archive.org");
    }
}
