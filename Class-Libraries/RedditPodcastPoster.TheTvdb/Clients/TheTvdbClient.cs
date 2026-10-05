using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.TheTvdb.Configuration;
using RedditPodcastPoster.TheTvdb.Models;

namespace RedditPodcastPoster.TheTvdb.Clients;

public sealed partial class TheTvdbClient(
    HttpClient httpClient,
    IOptions<TheTvdbOptions> options,
    ILogger<TheTvdbClient> logger,
    TheTvdbLoginSession session) : ITheTvdbClient
{
    /// <summary>
    /// TheTVDB source type for a TheMovieDB.com title id on a series or episode extended record.
    /// A collection uses the same source name and a different type (<c>themoviedb.org/collection</c>).
    /// </summary>
    private const int TheMovieDbTitleSourceType = 12;

    private const string TheMovieDbSourceName = "TheMovieDB.com";
    private const int MaxEpisodePages = 50;
    private readonly TheTvdbOptions _options = options.Value;

    public async Task<IReadOnlyList<TheTvdbSearchHit>> SearchAsync(
        string query,
        TheTvdbSearchType type,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var typeQuery = type == TheTvdbSearchType.Movie ? "movie" : "series";
        var path =
            $"search?query={Uri.EscapeDataString(query)}&type={typeQuery}";
        var envelope = await GetAsync<List<TheTvdbSearchHitDto>>(path, cancellationToken).ConfigureAwait(false);
        return (envelope?.Data ?? []).Select(hit => MapSearchHit(hit, type)).ToList();
    }

    public async Task<TheTvdbSeries?> GetSeriesAsync(long seriesId, CancellationToken cancellationToken = default)
    {
        var envelope = await GetAsync<TheTvdbSeriesDto>($"series/{seriesId}/extended", cancellationToken)
            .ConfigureAwait(false);
        return envelope?.Data is { } series ? MapSeries(series) : null;
    }

    public async Task<IReadOnlyList<TheTvdbEpisode>> GetEpisodesAsync(
        long seriesId,
        CancellationToken cancellationToken = default)
    {
        var episodes = new List<TheTvdbEpisode>();
        long? previousFirstId = null;
        for (var page = 0; page < MaxEpisodePages; page++)
        {
            var envelope = await GetAsync<TheTvdbEpisodePageDto>(
                    $"series/{seriesId}/episodes/default?page={page}",
                    cancellationToken)
                .ConfigureAwait(false);
            var pageData = envelope?.Data;
            var pageEpisodes = pageData?.Episodes;
            if (pageEpisodes is not { Count: > 0 })
            {
                break;
            }

            if (pageEpisodes[0].Id == previousFirstId)
            {
                break;
            }

            previousFirstId = pageEpisodes[0].Id;
            var slug = pageData?.Series?.Slug;
            episodes.AddRange(pageEpisodes.Select(episode => MapEpisode(episode, slug, parentSeriesTmdbId: null)));
        }

        return episodes;
    }

    public async Task<TheTvdbEpisode?> GetEpisodeAsync(long episodeId, CancellationToken cancellationToken = default)
    {
        var envelope = await GetAsync<TheTvdbEpisodeDto>($"episodes/{episodeId}/extended", cancellationToken)
            .ConfigureAwait(false);
        if (envelope?.Data is not { } episode)
        {
            return null;
        }

        string? slug = null;
        int? parentSeriesTmdbId = null;
        if (episode.SeriesId > 0)
        {
            // The extended series record carries the slug and the parent series TheMovieDB.com title id.
            // The episode row's TheMovieDB.com id is an episode id and is mapped separately.
            var series = await GetAsync<TheTvdbSeriesDto>($"series/{episode.SeriesId}/extended", cancellationToken)
                .ConfigureAwait(false);
            slug = series?.Data?.Slug;
            parentSeriesTmdbId = ReadTmdbTitleId(series?.Data?.RemoteIds);
        }

        return MapEpisode(episode, slug, parentSeriesTmdbId);
    }

    private async Task<TheTvdbEnvelope<T>?> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, relativePath, content: null, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "TheTVDB GET {path} failed with status {status}.",
                relativePath,
                (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<TheTvdbEnvelope<T>>(TheTvdbJson.Options, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(method, relativePath, content, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        session.Invalidate();
        return await SendOnceAsync(method, relativePath, content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await session.GetOrCreateAsync(LoginAsync, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
        {
            request.Content = content;
        }

        return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> LoginAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("TheTVDB ApiKey is not configured.");
        }

        var login = new TheTvdbLoginRequest
        {
            ApiKey = _options.ApiKey,
            Pin = string.IsNullOrWhiteSpace(_options.Pin) ? null : _options.Pin
        };
        using var response = await httpClient.PostAsJsonAsync("login", login, TheTvdbJson.Options, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("TheTVDB login failed with status {status}.", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var envelope = await response.Content
            .ReadFromJsonAsync<TheTvdbEnvelope<TheTvdbLoginData>>(TheTvdbJson.Options, cancellationToken)
            .ConfigureAwait(false);
        var token = envelope?.Data?.Token;
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TheTVDB login did not return a token.");
        }

        return token;
    }

    private static TheTvdbSearchHit MapSearchHit(TheTvdbSearchHitDto hit, TheTvdbSearchType requestedType)
    {
        var type = string.Equals(hit.Type, "movie", StringComparison.OrdinalIgnoreCase)
            ? TheTvdbSearchType.Movie
            : requestedType;
        _ = long.TryParse(hit.TvdbId, out var id);
        var imdbId = ReadImdbId(hit.RemoteIds);
        return new TheTvdbSearchHit(
            id,
            hit.Name ?? "",
            type,
            hit.Year,
            hit.Country,
            hit.Network,
            hit.Slug,
            imdbId,
            CanonicalUrl(type, hit.Slug),
            ImdbUrl(imdbId));
    }

    private static TheTvdbSeries MapSeries(TheTvdbSeriesDto series)
    {
        var imdbId = ReadImdbId(series.RemoteIds);
        return new TheTvdbSeries(
            series.Id,
            series.Name ?? "",
            series.Year,
            series.OriginalCountry,
            series.Slug,
            imdbId,
            ReadTmdbTitleId(series.RemoteIds),
            CanonicalUrl(TheTvdbSearchType.Series, series.Slug),
            ImdbUrl(imdbId));
    }

    private static TheTvdbEpisode MapEpisode(
        TheTvdbEpisodeDto episode,
        string? seriesSlug,
        int? parentSeriesTmdbId)
    {
        var imdbId = ReadImdbId(episode.RemoteIds);
        return new TheTvdbEpisode(
            episode.Id,
            episode.SeriesId,
            episode.Name ?? "",
            episode.SeasonNumber,
            episode.Number,
            episode.Year,
            imdbId,
            parentSeriesTmdbId,
            ReadTmdbTitleId(episode.RemoteIds),
            EpisodeUrl(seriesSlug, episode.Id),
            ImdbUrl(imdbId));
    }

    /// <summary>
    /// TheMovieDB.com title id (source type 12). Empty ids are ignored.
    /// A collection id uses the same source name and is not a title id.
    /// On a series record this is the series id. On an episode record this is the episode id.
    /// </summary>
    private static int? ReadTmdbTitleId(IEnumerable<TheTvdbRemoteId>? remoteIds)
    {
        if (remoteIds is null)
        {
            return null;
        }

        foreach (var remote in remoteIds)
        {
            if (remote.Type != TheMovieDbTitleSourceType)
            {
                continue;
            }

            if (!string.Equals(remote.SourceName, TheMovieDbSourceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(remote.Id))
            {
                continue;
            }

            if (int.TryParse(remote.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                return id;
            }
        }

        return null;
    }

    private static string? ReadImdbId(IEnumerable<TheTvdbRemoteId>? remoteIds) =>
        remoteIds?
            .FirstOrDefault(remote =>
                string.Equals(remote.SourceName, "imdb", StringComparison.OrdinalIgnoreCase)
                && ImdbTitleId().IsMatch(remote.Id ?? ""))
            ?.Id;

    private static Uri? CanonicalUrl(TheTvdbSearchType type, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var kind = type == TheTvdbSearchType.Movie ? "movies" : "series";
        return new Uri($"https://www.thetvdb.com/{kind}/{Uri.EscapeDataString(slug)}");
    }

    private static Uri? EpisodeUrl(string? seriesSlug, long episodeId)
    {
        if (string.IsNullOrWhiteSpace(seriesSlug) || episodeId <= 0)
        {
            return null;
        }

        return new Uri(
            $"https://www.thetvdb.com/series/{Uri.EscapeDataString(seriesSlug)}/episodes/{episodeId}");
    }

    private static Uri? ImdbUrl(string? imdbTitleId) =>
        imdbTitleId is null ? null : new Uri($"https://www.imdb.com/title/{imdbTitleId}/");

    [GeneratedRegex("^tt[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbTitleId();
}
