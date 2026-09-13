using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Tubi.Matching;

public static class TubiUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) => TryParse(url, out _, out _);

    public static Uri CanonicalUrl(Uri url) =>
        ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.Tubi, url);

    public static bool TryParse(Uri url, out string kind, out string id)
    {
        kind = null!;
        id = null!;
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "tubitv.com"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        if (parts.Length >= 2 &&
            IsLocaleSegment(parts[0]) &&
            TryNormalizeKind(parts[1], out _))
        {
            index = 1;
        }

        if (parts.Length < index + 2)
        {
            return false;
        }

        if (!TryNormalizeKind(parts[index], out kind))
        {
            return false;
        }

        var candidateId = parts[index + 1];
        if (candidateId.Length == 0 || !candidateId.All(char.IsDigit))
        {
            return false;
        }

        var remaining = parts.Length - (index + 2);
        if (remaining > 1)
        {
            return false;
        }

        id = candidateId;
        return true;
    }

    internal static bool TryNormalizeKind(string segment, out string kind)
    {
        if (segment.Equals("movies", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("movie", StringComparison.OrdinalIgnoreCase))
        {
            kind = "movies";
            return true;
        }

        if (segment.Equals("tv-shows", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("tv", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("shows", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("series", StringComparison.OrdinalIgnoreCase))
        {
            kind = "tv-shows";
            return true;
        }

        if (segment.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            kind = "video";
            return true;
        }

        kind = null!;
        return false;
    }

    internal static bool IsLocaleSegment(string segment)
    {
        if (segment.Length is not (2 or 5))
        {
            return false;
        }

        if (!char.IsAsciiLetter(segment[0]) || !char.IsAsciiLetter(segment[1]))
        {
            return false;
        }

        if (segment.Length == 2)
        {
            return true;
        }

        return segment[2] == '-' &&
               char.IsAsciiLetter(segment[3]) &&
               char.IsAsciiLetter(segment[4]);
    }
}
