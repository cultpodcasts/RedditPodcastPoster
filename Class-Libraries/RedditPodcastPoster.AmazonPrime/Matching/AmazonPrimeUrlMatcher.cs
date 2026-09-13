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

        if (IsAmazonHost(host))
        {
            return IsAmazonVideoPath(url.AbsolutePath);
        }

        return false;
    }

    public static bool IsCatalogUrl(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return false;
        }

        var host = ServiceCatalog.CanonicalHost(url);
        return ServiceCatalog.IsHost(host, "primevideo.com") ||
               (IsAmazonHost(host) && IsAmazonVideoPath(url.AbsolutePath));
    }

    public static bool IsAmazonHost(string host) =>
        host == "amazon.com" || host.EndsWith(".amazon.com", StringComparison.Ordinal) ||
        host == "amazon.co.uk" || host.EndsWith(".amazon.co.uk", StringComparison.Ordinal);

    private static bool IsAmazonVideoPath(string path) =>
        path.Contains("/gp/video", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/Prime-Video", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/prime-video", StringComparison.OrdinalIgnoreCase);
}
