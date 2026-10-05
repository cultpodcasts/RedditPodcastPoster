using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Authority;

public sealed class CatalogueAuthorityLookup(ITmdbClient tmdb, ITheTvdbClient theTvdb) : ICatalogueAuthorityLookup
{
    public async Task<CatalogueAuthorityIds> CollectFilmAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        // Films have no TheTVDB id. This lookup does not call TheTVDB.
        var movie = await tmdb.GetMovieAsync(tmdbId, cancellationToken).ConfigureAwait(false);
        var imdbId = ValidImdb(movie?.ImdbId);
        return new CatalogueAuthorityIds(
            imdbId,
            movie?.Id ?? tmdbId,
            TvdbId: null,
            imdbId is null ? null : CatalogueCanonicalId.ImdbPage(imdbId),
            Tvdb: null);
    }

    public async Task<CatalogueAuthorityIds> CollectTvShowAsync(
        int? tmdbId,
        long? tvdbId,
        CancellationToken cancellationToken = default)
    {
        if (tmdbId is null && tvdbId is not > 0)
        {
            throw new ArgumentException("A TV show lookup needs a TMDB series id or a TheTVDB series id.");
        }

        var tvdbSeries = tvdbId is long knownTvdbId and > 0
            ? await theTvdb.GetSeriesAsync(knownTvdbId, cancellationToken).ConfigureAwait(false)
            : null;
        // A caller-supplied TMDB series id wins. A TheTVDB-only show uses the series title id.
        var seriesTmdbId = tmdbId ?? PositiveTmdbId(tvdbSeries?.TmdbSeriesId);
        var series = seriesTmdbId is int seriesId
            ? await tmdb.GetTvSeriesAsync(seriesId, cancellationToken).ConfigureAwait(false)
            : null;
        if (tvdbSeries is null && series?.TvdbId is long discoveredTvdbId and > 0)
        {
            tvdbSeries = await theTvdb.GetSeriesAsync(discoveredTvdbId, cancellationToken).ConfigureAwait(false);
        }

        var imdbId = PreferImdb(series?.ImdbId, tvdbSeries?.ImdbTitleId);
        long? storedTvdbId = tvdbSeries is { Id: > 0 } knownSeries
            ? knownSeries.Id
            : tvdbId is > 0
                ? tvdbId
                : series?.TvdbId is > 0
                    ? series.TvdbId
                    : null;
        return new CatalogueAuthorityIds(
            imdbId,
            series?.Id ?? seriesTmdbId,
            storedTvdbId,
            imdbId is null ? null : CatalogueCanonicalId.ImdbPage(imdbId),
            TvdbPage(storedTvdbId, tvdbSeries?.CanonicalUrl, TmdbTitleKind.TvSeries));
    }

    public async Task<CatalogueAuthorityIds> CollectTvShowEpisodeAsync(
        int? tmdbSeriesId,
        int? seasonNumber,
        int? episodeNumber,
        long? tvdbEpisodeId,
        CancellationToken cancellationToken = default)
    {
        if (tvdbEpisodeId is not > 0 &&
            (tmdbSeriesId is null || seasonNumber is null || episodeNumber is null))
        {
            throw new ArgumentException(
                "A TV episode lookup needs a TheTVDB episode id or a TMDB series id with season and episode numbers.");
        }

        var tvdbEpisode = tvdbEpisodeId is long episodeId and > 0
            ? await theTvdb.GetEpisodeAsync(episodeId, cancellationToken).ConfigureAwait(false)
            : null;
        var season = seasonNumber ?? tvdbEpisode?.SeasonNumber;
        var number = episodeNumber ?? tvdbEpisode?.EpisodeNumber;
        var seriesTmdbId = await ResolveSeriesTmdbIdAsync(tmdbSeriesId, tvdbEpisode, cancellationToken)
            .ConfigureAwait(false);
        var tmdbEpisode = seriesTmdbId is int seriesId && season is int seasonNo && number is int episodeNo
            ? await tmdb.GetTvEpisodeAsync(seriesId, seasonNo, episodeNo, cancellationToken).ConfigureAwait(false)
            : null;
        if (tvdbEpisode is null && tmdbEpisode?.TvdbId is long discovered and > 0)
        {
            tvdbEpisode = await theTvdb.GetEpisodeAsync(discovered, cancellationToken).ConfigureAwait(false);
        }

        var imdbId = PreferImdb(tmdbEpisode?.ImdbId, tvdbEpisode?.ImdbTitleId);
        var storedTvdbId = tvdbEpisode?.Id is > 0
            ? tvdbEpisode.Id
            : tmdbEpisode?.TvdbId is > 0
                ? tmdbEpisode.TvdbId
                : tvdbEpisodeId is > 0
                    ? tvdbEpisodeId
                    : null;
        return new CatalogueAuthorityIds(
            imdbId,
            StoredEpisodeTmdbId(tmdbEpisode?.Id, tvdbEpisode?.TmdbEpisodeId),
            storedTvdbId,
            imdbId is null ? null : CatalogueCanonicalId.ImdbPage(imdbId),
            TvdbPage(storedTvdbId, tvdbEpisode?.CanonicalUrl, TmdbTitleKind.TvEpisode));
    }

    /// <summary>
    /// Parent series id for <see cref="ITmdbClient.GetTvEpisodeAsync"/>.
    /// TheMovieDB.com on the episode row is an episode id and is never passed here.
    /// </summary>
    private async Task<int?> ResolveSeriesTmdbIdAsync(
        int? callerSeriesTmdbId,
        TheTvdbEpisode? tvdbEpisode,
        CancellationToken cancellationToken)
    {
        if (callerSeriesTmdbId is int callerId)
        {
            return callerId;
        }

        if (tvdbEpisode is not { SeriesId: > 0 } episode)
        {
            return PositiveTmdbId(tvdbEpisode?.TmdbSeriesId);
        }

        var parent = await theTvdb.GetSeriesAsync(episode.SeriesId, cancellationToken).ConfigureAwait(false);
        return PositiveTmdbId(parent?.TmdbSeriesId) ?? PositiveTmdbId(episode.TmdbSeriesId);
    }

    /// <summary>
    /// The stored episode TMDB id is the TMDB payload id.
    /// A TheMovieDB.com id on the TheTVDB episode row is kept only when it matches that payload.
    /// </summary>
    private static int? StoredEpisodeTmdbId(int? payloadId, int? episodeRowTmdbId)
    {
        if (payloadId is not int id || id <= 0)
        {
            return null;
        }

        if (episodeRowTmdbId is int rowId && rowId == id)
        {
            return rowId;
        }

        return id;
    }

    private static int? PositiveTmdbId(int? id) => id is > 0 ? id : null;

    private static Uri? TvdbPage(long? tvdbId, Uri? canonicalUrl, TmdbTitleKind kind)
    {
        if (canonicalUrl is not null)
        {
            return canonicalUrl;
        }

        return tvdbId is > 0 ? CatalogueCanonicalId.TvdbPage(tvdbId.Value, kind) : null;
    }

    private static string? PreferImdb(string? first, string? second) =>
        ValidImdb(first) ?? ValidImdb(second);

    private static string? ValidImdb(string? imdbId) =>
        imdbId is not null && CatalogueCanonicalId.Resolve(imdbId, null, TmdbTitleKind.Movie) is not null
            ? imdbId
            : null;
}
