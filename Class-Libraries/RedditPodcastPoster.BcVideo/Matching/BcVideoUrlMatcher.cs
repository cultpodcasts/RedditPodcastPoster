using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.BcVideo.Matching;

public static class BcVideoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "bitchute.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!parts[0].Equals("video", StringComparison.OrdinalIgnoreCase) &&
            !parts[0].Equals("embed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsVideoId(parts[1]);
    }

    internal static bool IsVideoId(string part) =>
        part.Length >= 6 && part.All(char.IsLetterOrDigit);
}
