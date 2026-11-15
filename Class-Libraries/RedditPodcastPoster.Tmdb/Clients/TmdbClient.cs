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

    public async Task<TmdbSearchHit?> FindMovieByImdbIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        var valid = ValidImdbId(imdbId.Trim());
        if (valid is null)
        {
            return null;
        }

        var response = await GetAsync<TmdbFindResponse>(
                $"find/{Uri.EscapeDataString(valid)}?external_source=imdb_id",
                cancellationToken)
            .ConfigureAwait(false);
        var movie = response?.MovieResults?.FirstOrDefault();
        return movie is null ? null : MapHit(movie, TmdbTitleKind.Movie);
    }

    public async Task<TmdbSearchHit?> FindTvSeriesByImdbIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        var valid = ValidImdbId(imdbId.Trim());
        if (valid is null)
        {
            return null;
        }

        var response = await GetAsync<TmdbFindResponse>(
                $"find/{Uri.EscapeDataString(valid)}?external_source=imdb_id",
                cancellationToken)
            .ConfigureAwait(false);
        var series = response?.TvResults?.FirstOrDefault();
        return series is null ? null : MapHit(series, TmdbTitleKind.TvSeries);
    }

    public async Task<TmdbTitle?> GetMovieAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var details = await GetAsync<TmdbDetailsDto>(
                $"movie/{movieId.ToString(CultureInfo.InvariantCulture)}?append_to_response=external_ids",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null ? null : Map(details, TmdbTitleKind.Movie);
    }

    public async Task<TmdbMovieMetadata?> GetMovieMetadataAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync<TmdbMovieMetadataDto>(
                $"movie/{movieId.ToString(CultureInfo.InvariantCulture)}?append_to_response=alternative_titles,translations",
                cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        var title = Clean(details.Title) ?? string.Empty;
        var otherTitles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? candidate)
        {
            var clean = Clean(candidate);
            if (clean is not null && seen.Add(clean))
            {
                otherTitles.Add(clean);
            }
        }

        Add(details.Title);
        Add(details.OriginalTitle);
        foreach (var alternative in details.AlternativeTitles?.Titles ?? [])
        {
            Add(alternative.Title);
        }

        foreach (var translation in details.Translations?.Translations ?? [])
        {
            Add(translation.Data?.Title ?? translation.Data?.Name);
        }

        return new TmdbMovieMetadata(
            details.Id,
            title,
            Clean(details.OriginalTitle),
            Clean(details.Overview),
            details.Runtime is > 0 ? details.Runtime : null,
            DateOf(details.ReleaseDate),
            TmdbImage.Backdrop(details.BackdropPath) ?? TmdbImage.Poster(details.PosterPath),
            otherTitles,
            FirstNamed(details.ProductionCompanies));
    }

    public async Task<TmdbShowMetadata?> GetTvShowMetadataAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync<TmdbTvShowMetadataDto>(
                $"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null
            ? null
            : new TmdbShowMetadata(details.Id, FirstNamed(details.Networks) ?? FirstNamed(details.ProductionCompanies));
    }

    public async Task<TmdbEpisodeMetadata?> GetTvEpisodeMetadataAsync(
        int seriesId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seasonNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(episodeNumber);
        var details = await GetAsync<TmdbEpisodeMetadataDto>(
                $"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}/season/{seasonNumber.ToString(CultureInfo.InvariantCulture)}/episode/{episodeNumber.ToString(CultureInfo.InvariantCulture)}",
                cancellationToken)
            .ConfigureAwait(false);
        return details is null
            ? null
            : new TmdbEpisodeMetadata(
                details.Id,
                Clean(details.Name),
                Clean(details.Overview),
                details.Runtime is > 0 ? details.Runtime : null,
                DateOf(details.AirDate),
                TmdbImage.Still(details.StillPath));
    }

    public async Task<TmdbEpisodeReference?> FindTvEpisodeByImdbIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        var valid = ValidImdbId(imdbId.Trim());
        if (valid is null)
        {
            return null;
        }

        var response = await GetAsync<TmdbFindResponse>(
                $"find/{Uri.EscapeDataString(valid)}?external_source=imdb_id",
                cancellationToken)
            .ConfigureAwait(false);
        var episode = response?.TvEpisodeResults?.FirstOrDefault(
            item => item.ShowId > 0 && item.SeasonNumber is >= 0 && item.EpisodeNumber is >= 0);
        return episode is null
            ? null
            : new TmdbEpisodeReference(episode.ShowId, episode.SeasonNumber!.Value, episode.EpisodeNumber!.Value);
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
        ApplyCredential(request);
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

    /// <summary>
    /// A read-access token is a JWT and is sent as Bearer.
    /// A v3 API key has no dots and is sent as <c>api_key</c>. Sending that key as Bearer is a 401.
    /// </summary>
    private void ApplyCredential(HttpRequestMessage request)
    {
        var key = _options.ApiKey.Trim();
        if (key.Contains('.', StringComparison.Ordinal))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return;
        }

        var relative = request.RequestUri?.OriginalString ?? string.Empty;
        var separator = relative.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        request.RequestUri = new Uri(relative + separator + "api_key=" + Uri.EscapeDataString(key), UriKind.Relative);
    }

    private static TmdbSearchHit MapHit(TmdbListedTitleDto item, TmdbTitleKind kind)
    {
        var date = kind == TmdbTitleKind.Movie ? item.ReleaseDate : item.FirstAirDate;
        var name = item.Title ?? item.Name ?? "";
        var original = (item.OriginalTitle ?? item.OriginalName)?.Trim();
        return new TmdbSearchHit(
            item.Id,
            name,
            kind,
            YearOf(date),
            item.PosterPath,
            string.IsNullOrWhiteSpace(item.Overview) ? null : item.Overview.Trim(),
            string.IsNullOrWhiteSpace(original) || original.Equals(name, StringComparison.Ordinal) ? null : original);
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

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The first non-blank name in a TMDB network or production-company list (RPP #1436).</summary>
    private static string? FirstNamed(IEnumerable<TmdbNamedItemDto>? items)
    {
        if (items is null)
        {
            return null;
        }

        foreach (var item in items)
        {
            var name = Clean(item.Name);
            if (name is not null)
            {
                return name;
            }
        }

        return null;
    }

    private static DateOnly? DateOf(string? date) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static int? YearOf(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year > 0
            ? year
            : null;
}
