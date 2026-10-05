namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// Collects IMDb, TMDB, and TheTVDB ids.
/// Films use TMDB only. TV shows and episodes use TMDB and TheTVDB.
/// </summary>
public interface ICatalogueAuthorityLookup
{
    Task<CatalogueAuthorityIds> CollectFilmAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<CatalogueAuthorityIds> CollectTvShowAsync(
        int? tmdbId,
        long? tvdbId,
        CancellationToken cancellationToken = default);

    Task<CatalogueAuthorityIds> CollectTvShowEpisodeAsync(
        int? tmdbSeriesId,
        int? seasonNumber,
        int? episodeNumber,
        long? tvdbEpisodeId,
        CancellationToken cancellationToken = default);
}
