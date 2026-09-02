namespace RedditPodcastPoster.Netflix.Matching;

public static class NetflixUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        if (!host.Contains("netflix.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Contains("/title/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/watch/", StringComparison.OrdinalIgnoreCase);
    }
}
