using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Ard.Matching;

public static partial class ArdUrlMatcher
{
    /// <summary>
    /// Video: <c>/video/{show}/{episode}/{publisher}/{id}</c> or compact <c>/video/{id}</c>.
    /// Series hub: <c>/serie/{slug}/...</c> with a trailing id segment.
    /// Sendung hub: <c>/sendung/{slug}/{id}</c>.
    /// Browse shelves without an id are rejected.
    /// </summary>
    public static bool IsSubmitUrl(Uri url) =>
        IsVideoUrl(url) || IsSerieUrl(url) || IsSendungUrl(url);

    public static bool IsVideoUrl(Uri url)
    {
        if (!IsArdHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            return ContentIdRegex().IsMatch(parts[1]);
        }

        return parts.Length >= 5 &&
               IsCatalogueSegment(parts[1]) &&
               IsCatalogueSegment(parts[2]) &&
               IsCatalogueSegment(parts[3]) &&
               ContentIdRegex().IsMatch(parts[4]);
    }

    public static bool IsSerieUrl(Uri url)
    {
        if (!IsArdHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 &&
               parts[0].Equals("serie", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[1]) &&
               parts.Any(ContentIdRegex().IsMatch);
    }

    public static bool IsSendungUrl(Uri url)
    {
        if (!IsArdHost(url))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 &&
               parts[0].Equals("sendung", StringComparison.OrdinalIgnoreCase) &&
               IsCatalogueSegment(parts[1]) &&
               ContentIdRegex().IsMatch(parts[2]);
    }

    private static bool IsArdHost(Uri url) =>
        ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "ardmediathek.de");

    private static bool IsCatalogueSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains('.', StringComparison.Ordinal);

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{8,}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentIdRegex();
}
