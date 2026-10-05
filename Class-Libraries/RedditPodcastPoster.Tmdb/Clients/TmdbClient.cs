using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Tmdb.Configuration;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Tmdb.Clients;

public sealed class TmdbClient(
    HttpClient httpClient,
    IOptions<TmdbOptions> options,
    ILogger<TmdbClient> logger) : ITmdbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TmdbOptions _options = options.Value;

    public Task<IReadOnlyList<TmdbSearchHit>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        SearchAsync(query, "search/movie", TmdbTitleKind.Movie, cancellationToken);

    public Task<IReadOnlyList<TmdbSearchHit>> SearchTvAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        SearchAsync(query, "search/tv", TmdbTitleKind.TvSeries, cancellationToken);

    public async Task<TmdbTitle?> GetMovieAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var details = await GetAsync<TmdbDetailsDto>(
                $"movie/{movieId.ToString(CultureInfo.InvariantCulture)}?append_to_response=external_ids",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null ? null : Map(details, TmdbTitleKind.Movie);
    }

    public async Task<TmdbTitle?> GetTvSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var details = await GetAsync<TmdbDetailsDto>(
                $"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}?append_to_response=external_ids",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null ? null : Map(details, TmdbTitleKind.TvSeries);
    }

    public async Task<TmdbTitle?> GetTvEpisodeAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seasonNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(episodeNumber);
        var details = await GetAsync<TmdbDetailsDto>(
                $"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}/season/{seasonNumber.ToString(CultureInfo.InvariantCulture)}/episode/{episodeNumber.ToString(CultureInfo.InvariantCulture)}?append_to_response=external_ids",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null ? null : Map(details, TmdbTitleKind.TvEpisode);
    }

    private async Task<IReadOnlyList<TmdbSearchHit>> SearchAsync(
        string query,
        string path,
        TmdbTitleKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var response = await GetAsync<TmdbSearchResponse>(
                $"{path}?query={Uri.EscapeDataString(query)}",
                cancellationToken)
            .ConfigureAwait(false);
        return (response?.Results ?? []).Select(item => MapHit(item, kind)).ToList();
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("TMDB ApiKey is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("TMDB {Path} returned {Status}", path, (int)response.StatusCode);
            throw new InvalidOperationException($"TMDB request failed with status {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static TmdbSearchHit MapHit(TmdbListedTitleDto item, TmdbTitleKind kind)
    {
        var date = kind == TmdbTitleKind.Movie ? item.ReleaseDate : item.FirstAirDate;
        return new TmdbSearchHit(item.Id, item.Title ?? item.Name ?? "", kind, YearOf(date));
    }

    private static TmdbTitle Map(TmdbDetailsDto details, TmdbTitleKind kind)
    {
        var imdbId = ValidImdbId(details.ExternalIds?.ImdbId) ?? ValidImdbId(details.ImdbId);
        var tvdbId = details.ExternalIds?.TvdbId is > 0 ? details.ExternalIds.TvdbId : null;
        var canonical = CatalogueCanonicalId.Resolve(imdbId, tvdbId, kind);
        var date = kind switch
        {
            TmdbTitleKind.Movie => details.ReleaseDate,
            TmdbTitleKind.TvSeries => details.FirstAirDate,
            _ => details.AirDate
        };
        return new TmdbTitle(
            details.Id,
            details.Title ?? details.Name ?? "",
            kind,
            YearOf(date),
            details.SeasonNumber,
            details.EpisodeNumber,
            imdbId,
            tvdbId,
            imdbId is null ? null : CatalogueCanonicalId.ImdbPage(imdbId),
            kind == TmdbTitleKind.Movie || tvdbId is null
                ? null
                : CatalogueCanonicalId.TvdbPage(tvdbId.Value, kind),
            canonical);
    }

    private static string? ValidImdbId(string? imdbId) =>
        imdbId is not null && CatalogueCanonicalId.Resolve(imdbId, null, TmdbTitleKind.Movie) is not null
            ? imdbId
            : null;

    private static int? YearOf(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year > 0
            ? year
            : null;
}
