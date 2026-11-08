using System.Net;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Shared title predicate for catalogue length/date matching and catch-up same-title deferral:
/// HTML-decode, trim, then ordinal equals or either side contains the other
/// (<see cref="EpisodePlatformMatcher.FindCatalogueMatchByLength"/> /
/// <see cref="EpisodePlatformMatcher.FindCatalogueMatchByDate"/> first branch).
/// Empty titles after normalise never match via Contains (blank equals blank only).
/// </summary>
public static class EpisodeTitleMatch
{
    public static bool EqualsOrContains(string? left, string? right)
    {
        var normalisedLeft = Normalise(left);
        var normalisedRight = Normalise(right);
        if (normalisedLeft.Length == 0 || normalisedRight.Length == 0)
        {
            return normalisedLeft == normalisedRight;
        }

        return normalisedLeft == normalisedRight ||
               normalisedLeft.Contains(normalisedRight, StringComparison.Ordinal) ||
               normalisedRight.Contains(normalisedLeft, StringComparison.Ordinal);
    }

    public static string Normalise(string? title) =>
        WebUtility.HtmlDecode(title ?? string.Empty).Trim();
}
