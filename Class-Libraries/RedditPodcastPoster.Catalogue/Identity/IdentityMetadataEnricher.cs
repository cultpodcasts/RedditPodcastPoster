using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Identity;

public sealed class IdentityMetadataEnricher(
    ITmdbClient tmdb,
    ITheTvdbClient theTvdb,
    ILogger<IdentityMetadataEnricher> logger) : IIdentityMetadataEnricher
{
    public async Task<TmdbMovieMetadata?> FetchFilmAsync(
        int? tmdbId,
        string? imdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var movieId = tmdbId is > 0
                ? tmdbId
                : string.IsNullOrWhiteSpace(imdbId)
                    ? null
                    : (await tmdb.FindMovieByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false))?.Id;
            if (movieId is not int id)
            {
                return null;
            }

            return await tmdb.GetMovieMetadataAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Film identity metadata lookup failed. tmdbId={TmdbId} imdbId={ImdbId}",
                tmdbId,
                imdbId);
            return null;
        }
    }

    public async Task<TmdbShowMetadata?> FetchTvShowAsync(
        int? tmdbId,
        string? imdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var seriesId = tmdbId is > 0
                ? tmdbId
                : string.IsNullOrWhiteSpace(imdbId)
                    ? null
                    : (await tmdb.FindTvSeriesByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false))?.Id;
            if (seriesId is not int id)
            {
                return null;
            }

            return await tmdb.GetTvShowMetadataAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "TV show identity metadata lookup failed. tmdbId={TmdbId} imdbId={ImdbId}",
                tmdbId,
                imdbId);
            return null;
        }
    }

    public async Task<TmdbEpisodeMetadata?> FetchTvShowEpisodeAsync(
        TvShowEpisode episode,
        int? showTmdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episode);
        try
        {
            var reference = await ResolveAsync(episode, showTmdbId, cancellationToken).ConfigureAwait(false);
            return reference is null
                ? null
                : await tmdb.GetTvEpisodeMetadataAsync(
                        reference.SeriesId,
                        reference.SeasonNumber,
                        reference.EpisodeNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "TV episode identity metadata lookup failed. episodeId={EpisodeId} tvdbId={TvdbId} imdbId={ImdbId}",
                episode.Id,
                episode.TvdbId,
                episode.ImdbId);
            return null;
        }
    }

    private async Task<TmdbEpisodeReference?> ResolveAsync(
        TvShowEpisode episode,
        int? showTmdbId,
        CancellationToken cancellationToken)
    {
        if (episode.TvdbId is long tvdbId and > 0)
        {
            var tvdbEpisode = await theTvdb.GetEpisodeAsync(tvdbId, cancellationToken).ConfigureAwait(false);
            var seriesId = showTmdbId is > 0 ? showTmdbId : tvdbEpisode?.TmdbSeriesId is > 0 ? tvdbEpisode.TmdbSeriesId : null;
            if (seriesId is int series &&
                tvdbEpisode?.SeasonNumber is int season and >= 0 &&
                tvdbEpisode.EpisodeNumber is int number and >= 0)
            {
                return new TmdbEpisodeReference(series, season, number);
            }
        }

        return string.IsNullOrWhiteSpace(episode.ImdbId)
            ? null
            : await tmdb.FindTvEpisodeByImdbIdAsync(episode.ImdbId, cancellationToken).ConfigureAwait(false);
    }
}
