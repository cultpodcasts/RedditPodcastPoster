namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// Title search used by identify dry-run. Films search TMDB movies.
/// TV searches TMDB series, then the authority lookup fills TheTVDB when the hit is unique.
/// </summary>
public interface ICatalogueAuthorityPreviewer
{
    Task<CatalogueAuthorityPreview> PreviewFilmAsync(string name, CancellationToken cancellationToken = default);

    Task<CatalogueAuthorityPreview> PreviewTvShowAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// #1387: for a TV show the TV authorities could not place, the same title as a film or documentary (IMDb films
    /// and TMDB movies) or as an IMDb series. Every hit is a low-confidence candidate
    /// (<see cref="CatalogueAuthorityResolution.DifferentTitle"/>) for the curator to pick; nothing is applied unasked.
    /// A film candidate is keyed by its IMDb id only, because a TMDB movie id is not a TMDB TV id.
    /// </summary>
    Task<CatalogueAuthorityPreview> PreviewTvShowOtherKindsAsync(
        string name,
        CancellationToken cancellationToken = default);
}
