using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.AmazonPrime.Matching;

public static class AmazonPrimeUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        if (ServiceCatalog.IsHost(host, "primevideo.com"))
        {
            return url.AbsolutePath.Contains("/detail/", StringComparison.OrdinalIgnoreCase) ||
                   url.AbsolutePath.Contains("/gp/video", StringComparison.OrdinalIgnoreCase);
        }

        if (ServiceCatalog.IsAmazonHost(host))
        {
            var path = url.AbsolutePath;
            return path.Contains("/gp/video", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/Prime-Video", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/prime-video", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
