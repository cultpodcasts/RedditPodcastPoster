namespace RedditPodcastPoster.AmazonPrime.Matching;

public static class AmazonPrimeUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        if (host.Contains("primevideo.com"))
        {
            return url.AbsolutePath.Contains("/detail/", StringComparison.OrdinalIgnoreCase) ||
                   url.AbsolutePath.Contains("/gp/video", StringComparison.OrdinalIgnoreCase);
        }

        if (host.Contains("amazon.com") || host.Contains("amazon.co.uk"))
        {
            var path = url.AbsolutePath;
            return path.Contains("/gp/video", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/Prime-Video", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/prime-video", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
