using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.DiscoveryPlus.Matching;

public static class DiscoveryPlusUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "discoveryplus.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if ((parts[i].Equals("show", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("video", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("movie", StringComparison.OrdinalIgnoreCase)) &&
                parts[i + 1].Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
