using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.Tubi.Matching;

public static class TubiUrlMatcher
{
    public static bool IsSubmitUrl(Uri url) => TryCompactPayload(url) != null;

    public static Uri CanonicalUrl(Uri url)
    {
        var compact = TryCompactPayload(url);
        return compact is null ? url : TryExpandPayload(compact) ?? url;
    }

    public static string? TryCompactPayload(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "tubitv.com"))
        {
            return null;
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
            return null;
        }

        if (!TryNormalizeKind(parts[index], out var kind))
        {
            return null;
        }

        var id = parts[index + 1];
        if (id.Length == 0 || !id.All(char.IsDigit))
        {
            return null;
        }

        var remaining = parts.Length - (index + 2);
        if (remaining > 1)
        {
            return null;
        }

        return $"{kind}/{id}";
    }

    public static Uri? TryExpandPayload(string payload) =>
        StreamingUrlCodecs.TryCreate($"https://tubitv.com/{payload}");

    private static bool TryNormalizeKind(string segment, out string kind)
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

    private static bool IsLocaleSegment(string segment)
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
