using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PlaySuisse.Matching;

public static class PlaySuisseUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "playsuisse.ch"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var localeOffset = parts[0].Length is 2 ? 1 : 0;
        if (parts.Length <= localeOffset + 1)
        {
            return false;
        }

        var kind = parts[localeOffset];
        var id = parts[localeOffset + 1];
        return (kind.Equals("watch", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("detail", StringComparison.OrdinalIgnoreCase)) &&
               id.Any(char.IsDigit);
    }
}
