namespace RedditPodcastPoster.BBC.Matching;

public static class BBCUrlMatcher
{
    public static bool IsBBCUrl(Uri url)
    {
        return url.Host.Contains("bbc.co.uk");
    }
}
