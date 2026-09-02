using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Vimeo.Matching;

public static class VimeoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "vimeo.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return IsNumericVideoId(parts[0]);
        }

        return parts.Length == 2 &&
               parts[0].Equals("video", StringComparison.OrdinalIgnoreCase) &&
               IsNumericVideoId(parts[1]);
    }

    private static bool IsNumericVideoId(string part) =>
        part.Length > 0 && part.All(char.IsDigit);
}
