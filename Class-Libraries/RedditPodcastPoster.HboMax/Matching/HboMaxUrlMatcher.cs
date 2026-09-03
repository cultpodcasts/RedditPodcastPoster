using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.HboMax.Matching;

public static class HboMaxUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        if (!ServiceCatalog.IsHost(host, "max.com") &&
            !ServiceCatalog.IsHost(host, "hbomax.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if ((parts[i].Equals("shows", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("show", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("movies", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("movie", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("series", StringComparison.OrdinalIgnoreCase)) &&
                parts[i + 1].Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
