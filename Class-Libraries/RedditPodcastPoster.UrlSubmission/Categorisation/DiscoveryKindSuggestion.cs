using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// How the curator answered a discovery kind-suggestion prompt. Unset means they have not answered.
/// Continue means keep the item as a podcast. Select means file it under the chosen candidate.
/// </summary>
public enum DiscoveryKindChoice
{
    Unset = 0,
    Select,
    Continue,
    Cancel
}

public static class DiscoveryKindChoices
{
    public static DiscoveryKindChoice Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "select" => DiscoveryKindChoice.Select,
            "continue" => DiscoveryKindChoice.Continue,
            "cancel" => DiscoveryKindChoice.Cancel,
            _ => DiscoveryKindChoice.Unset
        };
}

/// <summary>
/// A suggest-only kind hint for an unmapped discovery item. Nothing is applied until the curator
/// selects a candidate. Continue keeps the item as a podcast.
/// </summary>
public sealed record DiscoveryKindHold(
    string Target,
    string Query,
    IReadOnlyList<CatalogueAuthorityCandidate> Candidates,
    Guid? DiscoveryItemId = null);

/// <summary>
/// What to put on <see cref="SubmitOptions"/> when the curator selected a kind candidate.
/// Null signals mean the podcast path.
/// </summary>
public sealed record DiscoveryKindApplication(
    SubmitClassificationSignals Signals,
    CatalogueParentTarget? Parent = null,
    Guid? TargetFilmId = null,
    Guid? TargetNewsOrganisationId = null,
    Guid? TargetNewsReportId = null,
    CatalogueIdentityChoice IdentityChoice = CatalogueIdentityChoice.Unset,
    string? SelectedIdentityKey = null);

/// <summary>Opaque keys sent back as <c>selectedKindKeys</c>.</summary>
public static class DiscoveryKindKeys
{
    public const string CatalogueSource = "catalogue";
    public const string FilmPrefix = "catalogue:film:";
    public const string TvShowPrefix = "catalogue:tvshow:";
    public const string NewsReportPrefix = "catalogue:newsreport:";

    public static string Film(Guid id) => FilmPrefix + id.ToString("D");

    public static string TvShow(Guid id) => TvShowPrefix + id.ToString("D");

    public static string NewsReport(Guid organisationId, Guid reportId) =>
        NewsReportPrefix + organisationId.ToString("D") + ":" + reportId.ToString("D");

    public static bool TryParseFilm(string? key, out Guid filmId)
    {
        filmId = default;
        return TryParsePrefixedGuid(key, FilmPrefix, out filmId);
    }

    public static bool TryParseTvShow(string? key, out Guid showId)
    {
        showId = default;
        return TryParsePrefixedGuid(key, TvShowPrefix, out showId);
    }

    public static bool TryParseNewsReport(string? key, out Guid organisationId, out Guid reportId)
    {
        organisationId = default;
        reportId = default;
        var trimmed = key?.Trim();
        if (trimmed is null || !trimmed.StartsWith(NewsReportPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = trimmed[NewsReportPrefix.Length..];
        var parts = rest.Split(':', 2, StringSplitOptions.None);
        return parts.Length == 2 &&
               Guid.TryParse(parts[0], out organisationId) &&
               Guid.TryParse(parts[1], out reportId);
    }

    private static bool TryParsePrefixedGuid(string? key, string prefix, out Guid id)
    {
        id = default;
        var trimmed = key?.Trim();
        return trimmed is not null &&
               trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(trimmed[prefix.Length..], out id);
    }
}