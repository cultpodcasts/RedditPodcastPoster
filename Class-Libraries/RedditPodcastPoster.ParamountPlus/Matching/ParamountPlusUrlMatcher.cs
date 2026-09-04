using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.ParamountPlus.Matching;

public static class ParamountPlusUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "paramountplus.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if ((parts[i].Equals("shows", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("movies", StringComparison.OrdinalIgnoreCase) ||
                 parts[i].Equals("video", StringComparison.OrdinalIgnoreCase)) &&
                parts[i + 1].Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
