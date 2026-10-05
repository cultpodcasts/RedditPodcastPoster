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

        var series = tmdbId is int id
            ? await tmdb.GetTvSeriesAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        var resolvedTvdbId = tvdbId is > 0 ? tvdbId : series?.TvdbId;
        var tvdbSeries = resolvedTvdbId is long theTvdbId and > 0
            ? await theTvdb.GetSeriesAsync(theTvdbId, cancellationToken).ConfigureAwait(false)
            : null;
        var imdbId = PreferImdb(series?.ImdbId, tvdbSeries?.ImdbTitleId);
        long? storedTvdbId = tvdbSeries?.Id is > 0
            ? tvdbSeries!.Id
            : resolvedTvdbId is > 0 ? resolvedTvdbId : null;
        return new CatalogueAuthorityIds(
            imdbId,
            series?.Id ?? tmdbId,
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
        var tmdbEpisode = tmdbSeriesId is int seriesId && season is int seasonNo && number is int episodeNo
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
            tmdbEpisode?.Id,
            storedTvdbId,
            imdbId is null ? null : CatalogueCanonicalId.ImdbPage(imdbId),
            TvdbPage(storedTvdbId, tvdbEpisode?.CanonicalUrl, TmdbTitleKind.TvEpisode));
    }

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
