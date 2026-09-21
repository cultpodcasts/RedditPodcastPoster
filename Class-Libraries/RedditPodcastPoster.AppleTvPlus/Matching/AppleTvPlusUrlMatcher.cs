using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.AppleTvPlus.Matching;

public static partial class AppleTvPlusUrlMatcher
{
    /// <summary>
    /// Show: <c>/{storefront}/show/{slug}/{umc.cmc.*}</c>.
    /// Movie: <c>/{storefront}/movie/{slug}/{umc.cmc.*}</c>.
    /// Episode: <c>/{storefront}/episode/{slug}/{umc.cmc.*}</c>.
    /// Channel, shelf, clip, and person pages are not catalogue submit URLs.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsShowUrl(url) || IsMovieUrl(url) || IsEpisodeUrl(url);

    public static bool IsShowUrl(Uri url) =>
        TryMatchKind(url, "show");

    public static bool IsMovieUrl(Uri url) =>
        TryMatchKind(url, "movie");

    public static bool IsEpisodeUrl(Uri url) =>
        TryMatchKind(url, "episode");

    private static bool TryMatchKind(Uri url, string kind)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "tv.apple.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 &&
               StorefrontRegex().IsMatch(parts[0]) &&
               parts[1].Equals(kind, StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[2]) &&
               UmContentIdRegex().IsMatch(parts[3]);
    }

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    [GeneratedRegex("^[a-z]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StorefrontRegex();

    [GeneratedRegex(@"^umc\.cmc\.[a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UmContentIdRegex();
}
