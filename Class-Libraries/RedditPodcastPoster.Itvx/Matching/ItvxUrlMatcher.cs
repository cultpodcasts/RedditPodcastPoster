using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Itvx.Matching;

public static class ItvxUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "itv.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts[1].Equals("news", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return parts[1].Length > 0 && parts[2].Length > 0;
    }
}
