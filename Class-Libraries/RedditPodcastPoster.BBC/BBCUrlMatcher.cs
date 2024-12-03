namespace RedditPodcastPoster.BBC;

public static class BBCUrlMatcher
{
    public static bool IsBBCUrl(Uri url)
    {
        return url.Host.Contains("bbc.co.uk");
    }
}