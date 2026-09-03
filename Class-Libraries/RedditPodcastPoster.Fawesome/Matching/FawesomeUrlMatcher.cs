using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Fawesome.Matching;

public static class FawesomeUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "fawesome.tv"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var kind = parts[0];
        if (!kind.Equals("movies", StringComparison.OrdinalIgnoreCase) &&
            !kind.Equals("tv-shows", StringComparison.OrdinalIgnoreCase) &&
            !kind.Equals("tv", StringComparison.OrdinalIgnoreCase) &&
            !kind.Equals("shows", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return parts[1].Length > 0 && parts[1].Any(char.IsDigit);
    }
}
