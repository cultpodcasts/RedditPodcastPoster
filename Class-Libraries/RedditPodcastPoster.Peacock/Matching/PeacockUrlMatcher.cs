using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Peacock.Matching;

public static partial class PeacockUrlMatcher
{
    /// <summary>
    /// US SEO catalogue page: <c>/watch-online/movies|{tv}/...</c>.
    /// Asset page: <c>/watch/asset/{kind}/{slug}/{id}</c> (optional episode path).
    /// Playback page: <c>/watch/playback/vod/{id}</c>.
    /// App shells such as <c>/watch/home</c> are not catalogue items.
    /// Prefer <see cref="CanonicalUrl"/> / <see cref="TryToWatchOnlineUrl"/> before US scrape.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsWatchOnlineUrl(url) || IsAssetUrl(url) || IsPlaybackUrl(url);

    /// <summary>
    /// Rewrites signed-in <c>/watch/asset/...</c> to public <c>/watch-online/...</c>
    /// when the path is a catalogue title or episode. Otherwise returns the input.
    /// </summary>
    public static Uri CanonicalUrl(Uri url) =>
        TryToWatchOnlineUrl(url) ?? url;

    /// <summary>
    /// Public SEO twin for an asset catalogue URL, or null when already SEO / not rewritable.
    /// </summary>
    public static Uri? TryToWatchOnlineUrl(Uri url)
    {
        if (!IsPeacockHost(url) || !IsAssetUrl(url))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rawKind = parts[2];
        var kind = rawKind.Equals("movie", StringComparison.OrdinalIgnoreCase)
            ? "movies"
            : rawKind;
        if (!kind.Equals("tv", StringComparison.OrdinalIgnoreCase) &&
            !kind.Equals("movies", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var seoParts = new string[parts.Length - 1];
        seoParts[0] = "watch-online";
        seoParts[1] = kind;
        Array.Copy(parts, 3, seoParts, 2, parts.Length - 3);
        var builder = new UriBuilder(url)
        {
            Path = "/" + string.Join('/', seoParts),
            Query = string.Empty,
            Fragment = string.Empty
        };
        var candidate = builder.Uri;
        return IsWatchOnlineUrl(candidate) ? candidate : null;
    }

    /// <summary>
    /// Public SEO pages that SSR title/og meta (geo: US). Preferred prepare URLs.
    /// </summary>
    public static bool IsWatchOnlineUrl(Uri url)
    {
        if (!IsPeacockHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 ||
            !parts[0].Equals("watch-online", StringComparison.OrdinalIgnoreCase) ||
            !IsCatalogueSegment(parts[2]) ||
            !IsAssetId(parts[3]))
        {
            return false;
        }

        if (parts[1].Equals("movies", StringComparison.OrdinalIgnoreCase))
        {
            return parts.Length == 4;
        }

        if (!parts[1].Equals("tv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 4)
        {
            return true;
        }

        return parts.Length >= 9 &&
               parts[4].Equals("seasons", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[5]) &&
               parts[6].Equals("episodes", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[7]) &&
               IsAssetId(parts[8]);
    }

    public static bool IsAssetUrl(Uri url)
    {
        if (!IsPeacockHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 ||
            !parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals("asset", StringComparison.OrdinalIgnoreCase) ||
            !IsCatalogueSegment(parts[2]) ||
            !IsCatalogueSegment(parts[3]) ||
            !IsAssetId(parts[4]))
        {
            return false;
        }

        if (parts.Length == 5)
        {
            return true;
        }

        // Episode deep link under asset (same shape as watch-online).
        return parts.Length >= 10 &&
               parts[5].Equals("seasons", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[6]) &&
               parts[7].Equals("episodes", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[8]) &&
               IsAssetId(parts[9]);
    }

    public static bool IsPlaybackUrl(Uri url)
    {
        if (!IsPeacockHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /watch/playback/vod/{gmoOr_}[/{streamId}]
        if (parts.Length < 4 ||
            !parts[0].Equals("watch", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals("playback", StringComparison.OrdinalIgnoreCase) ||
            !parts[2].Equals("vod", StringComparison.OrdinalIgnoreCase) ||
            !IsPlaybackVodSegment(parts[3]))
        {
            return false;
        }

        if (parts.Length == 4)
        {
            return true;
        }

        return parts.Length == 5 && IsAssetId(parts[4]);
    }

    private static bool IsPeacockHost(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "peacocktv.com");

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    private static bool IsAssetId(string segment) =>
        NumericIdRegex().IsMatch(segment) || UuidIdRegex().IsMatch(segment);

    private static bool IsPlaybackVodSegment(string segment) =>
        segment.Equals("_", StringComparison.Ordinal) ||
        IsCatalogueSegment(segment);

    [GeneratedRegex(@"^\d{6,}$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericIdRegex();

    [GeneratedRegex(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidIdRegex();
}
