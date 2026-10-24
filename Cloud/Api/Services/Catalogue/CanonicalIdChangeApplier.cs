using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>
/// PATCH for catalogue identity ids: <c>imdbId</c>, <c>tmdbId</c>, <c>tvdbId</c>.
/// Omitted = unchanged; blank / 0 clears; invalid shape is a 400 before any write.
/// </summary>
internal static partial class CanonicalIdChangeApplier
{
    [GeneratedRegex(@"^tt\d{7,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdPattern();

    public static bool TryValidate(string? imdbId, int? tmdbId, long? tvdbId, out string? error)
    {
        error = null;
        if (imdbId is { } id && id.Trim().Length > 0 && !ImdbIdPattern().IsMatch(id.Trim()))
        {
            error = "imdbId must be 'tt' followed by 7 to 10 digits";
            return false;
        }

        if (tmdbId is < 0)
        {
            error = "tmdbId must be positive (0 clears it)";
            return false;
        }

        if (tvdbId is < 0)
        {
            error = "tvdbId must be positive (0 clears it)";
            return false;
        }

        return true;
    }

    public static void Apply(ITvCanonical document, string? imdbId, int? tmdbId, long? tvdbId)
    {
        if (imdbId is not null)
        {
            document.ImdbId = imdbId.Trim().Length == 0 ? null : imdbId.Trim();
        }

        if (tmdbId is not null)
        {
            document.TmdbId = tmdbId.Value == 0 ? null : tmdbId.Value;
        }

        if (tvdbId is not null)
        {
            document.TvdbId = tvdbId.Value == 0 ? null : tvdbId.Value;
        }
    }
}
