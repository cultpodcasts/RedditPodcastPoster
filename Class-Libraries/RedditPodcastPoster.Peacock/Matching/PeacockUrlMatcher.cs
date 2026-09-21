using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Peacock.Matching;

public static partial class PeacockUrlMatcher
{
    /// <summary>
    /// Asset page: <c>/watch/asset/{kind}/{slug}/{numericId}</c>.
    /// Playback page: <c>/watch/playback/vod/{id}</c>.
    /// App shells such as <c>/watch/home</c> are not catalogue items.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsAssetUrl(url) || IsPlaybackUrl(url);

    public static bool IsAssetUrl(Uri url)
    {
        if (!IsPeacockHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 5 &&
               parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase) &&
               parts[1].Equals("asset", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[2]) &&
               IsCatalogueSegment(parts[3]) &&
               NumericIdRegex().IsMatch(parts[4]);
    }

    public static bool IsPlaybackUrl(Uri url)
    {
        if (!IsPeacockHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 &&
               parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase) &&
               parts[1].Equals("playback", StringComparison.OrdinalIgnoreCase) &&
               parts[2].Equals("vod", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[3]);
    }

    private static bool IsPeacockHost(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "peacocktv.com");

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    [GeneratedRegex(@"^\d{6,}$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericIdRegex();
}
