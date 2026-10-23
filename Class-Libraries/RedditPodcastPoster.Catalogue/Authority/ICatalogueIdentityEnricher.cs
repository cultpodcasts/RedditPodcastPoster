using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// Resolves IMDb / TMDB / TheTVDB identity for catalogue creates.
/// A unique confident match supplies ids. Several matches hold for a curator choice.
/// </summary>
public interface ICatalogueIdentityEnricher
{
    Task<CatalogueIdentityAdmission> ResolveFilmAsync(
        string title,
        int? year,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default);

    Task<CatalogueIdentityAdmission> ResolveTvShowAsync(
        string name,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills blank show identity when needed, then tries a confident episode match within that show.
    /// </summary>
    Task<CatalogueIdentityAdmission> ResolveTvShowEpisodeAsync(
        TvShow show,
        string episodeTitle,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default);

    /// <summary>Applies resolved ids onto blank fields only. Returns whether anything changed.</summary>
    bool ApplyMissing(Film film, CatalogueAuthorityIds? ids);

    /// <summary>Applies resolved ids onto blank fields only. Returns whether anything changed.</summary>
    bool ApplyMissing(ITvCanonical document, CatalogueAuthorityIds? ids);
}
