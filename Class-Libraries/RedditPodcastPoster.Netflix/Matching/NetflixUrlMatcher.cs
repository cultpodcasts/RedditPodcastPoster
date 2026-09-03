using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Netflix.Matching;

public static class NetflixUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "netflix.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Contains("/title/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/watch/", StringComparison.OrdinalIgnoreCase);
    }
}
