using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.DisneyPlus.Matching;

public static class DisneyPlusUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "disneyplus.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        if (path.Contains("/browse/entity-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if ((parts[i].Equals("series", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("movies", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("play", StringComparison.OrdinalIgnoreCase)) &&
                parts[i + 1].Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
