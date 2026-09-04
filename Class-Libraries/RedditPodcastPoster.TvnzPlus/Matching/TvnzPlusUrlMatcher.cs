using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.TvnzPlus.Matching;

public static class TvnzPlusUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "tvnz.co.nz"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 &&
               parts[0].Equals("shows", StringComparison.OrdinalIgnoreCase) &&
               parts[1].Length > 0;
    }
}
