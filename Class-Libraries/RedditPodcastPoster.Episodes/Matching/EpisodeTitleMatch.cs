using System.Net;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Shared title predicate for catalogue length matching and catch-up same-title deferral:
/// HTML-decode, trim, then ordinal equals or either side contains the other
/// (<see cref="EpisodePlatformMatcher.FindCatalogueMatchByLength"/> first branch).
/// </summary>
public static class EpisodeTitleMatch
{
    public static bool EqualsOrContains(string? left, string? right)
    {
        var normalisedLeft = Normalise(left);
        var normalisedRight = Normalise(right);
        return normalisedLeft == normalisedRight ||
               normalisedLeft.Contains(normalisedRight) ||
               normalisedRight.Contains(normalisedLeft);
    }

    public static string Normalise(string? title) =>
        WebUtility.HtmlDecode(title ?? string.Empty).Trim();
}
