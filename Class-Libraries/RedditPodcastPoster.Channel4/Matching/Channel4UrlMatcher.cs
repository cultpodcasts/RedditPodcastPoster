using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Channel4.Matching;

public static class Channel4UrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        if (!ServiceCatalog.IsHost(host, "channel4.com") &&
            !ServiceCatalog.IsHost(host, "all4.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].Equals("programmes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            return true;
        }

        return parts.Length >= 4 &&
               parts[2].Equals("on-demand", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(parts[3]);
    }
}
