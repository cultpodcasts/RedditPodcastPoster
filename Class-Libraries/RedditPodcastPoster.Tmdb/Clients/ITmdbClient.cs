using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Tmdb.Clients;

public interface ITmdbClient
{
    Task<IReadOnlyList<TmdbSearchHit>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbSearchHit>> SearchTvAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// TMDB <c>find</c> by IMDb title id (<c>tt…</c>). Returns the first movie result, or null when TMDB has none.
    /// </summary>
    Task<TmdbSearchHit?> FindMovieByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TMDB <c>find</c> by IMDb title id for a TV series. Returns the first TV result, or null when TMDB has none.
    /// </summary>
    Task<TmdbSearchHit?> FindTvSeriesByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetMovieAsync(int movieId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The movie's own metadata: overview, runtime, release date, images, and every other title it is known by
    /// (original title, alternative titles and translated titles). Null when TMDB has no such movie.
    /// </summary>
    Task<TmdbMovieMetadata?> GetMovieMetadataAsync(int movieId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The series' publisher: the first named network, else the first named production company. Null when TMDB has no
    /// such series.
    /// </summary>
    Task<TmdbShowMetadata?> GetTvShowMetadataAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The episode's own metadata: name, overview, runtime, air date and still. Null when TMDB has no such episode.
    /// </summary>
    Task<TmdbEpisodeMetadata?> GetTvEpisodeMetadataAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TMDB <c>find</c> by IMDb title id for a TV episode: its series id, season and episode number. Null when TMDB
    /// has no episode for that id.
    /// </summary>
    Task<TmdbEpisodeReference?> FindTvEpisodeByImdbIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetTvSeriesAsync(int seriesId, CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetTvEpisodeAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
