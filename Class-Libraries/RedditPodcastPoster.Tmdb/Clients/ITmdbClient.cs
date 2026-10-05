using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Tmdb.Clients;

public interface ITmdbClient
{
    Task<IReadOnlyList<TmdbSearchHit>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbSearchHit>> SearchTvAsync(string query, CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetMovieAsync(int movieId, CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetTvSeriesAsync(int seriesId, CancellationToken cancellationToken = default);

    Task<TmdbTitle?> GetTvEpisodeAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
