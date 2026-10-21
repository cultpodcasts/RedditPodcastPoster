namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// Title search used by identify dry-run. Films search TMDB movies.
/// TV searches TMDB series, then the authority lookup fills TheTVDB when the hit is unique.
/// </summary>
public interface ICatalogueAuthorityPreviewer
{
    Task<CatalogueAuthorityPreview> PreviewFilmAsync(string name, CancellationToken cancellationToken = default);

    Task<CatalogueAuthorityPreview> PreviewTvShowAsync(string name, CancellationToken cancellationToken = default);
}
