using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <inheritdoc cref="IAppleCatalogueLookup" />
public sealed class AppleCatalogueLookup(
    IHttpClientFactory httpClientFactory,
    ICatalogueRequestGuard requestGuard,
    ILogger<AppleCatalogueLookup> logger) : IAppleCatalogueLookup, IPodcastPassApiCacheSource
{
    public const string HttpClientName = "itunes-catalogue";
    public const int SearchLimit = 50;
    private const string BaseUrl = "https://itunes.apple.com";

    private readonly ConcurrentDictionary<long, int> _counts = new();

    public async Task<int?> GetEpisodeCount(long podcastId, CancellationToken cancellationToken = default)
    {
        if (_counts.TryGetValue(podcastId, out var cached))
        {
            return cached;
        }

        var response = await Get($"{BaseUrl}/lookup?id={podcastId}", $"iTunesLookup podcast='{podcastId}'", cancellationToken);
        var count = response?.Results.FirstOrDefault(x => x.TrackCount is > 0)?.TrackCount;
        if (count is { } value)
        {
            _counts[podcastId] = value;
        }

        return count;
    }

    public async Task<IReadOnlyList<AppleEpisode>> SearchShowEpisodes(
        long podcastId, string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        var uri = $"{BaseUrl}/search?term={Uri.EscapeDataString(title.Trim())}&entity=podcastEpisode&limit={SearchLimit}&country=us";
        var response = await Get(uri, $"iTunesSearch podcast='{podcastId}'", cancellationToken);
        return response?.Results
            .Where(x => x.CollectionId == podcastId && x.TrackId is > 0 && !string.IsNullOrWhiteSpace(x.TrackName))
            .Select(ToAppleEpisode)
            .ToList() ?? [];
    }

    public void ClearPassCache() => _counts.Clear();

    private async Task<ITunesResponse?> Get(string uri, string operation, CancellationToken cancellationToken) =>
        await requestGuard.Run<ITunesResponse>(ApplePodcastService.Platform, operation, async ct =>
        {
            try
            {
                using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(uri, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("iTunes catalogue request '{Uri}' returned '{StatusCode}'.", uri, response.StatusCode);
                    return null;
                }

                return JsonSerializer.Deserialize<ITunesResponse>(await response.Content.ReadAsStringAsync(ct));
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "iTunes catalogue request '{Uri}' failed.", uri);
                return null;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "iTunes catalogue request '{Uri}' returned unreadable JSON.", uri);
                return null;
            }
        }, cancellationToken);

    private static AppleEpisode ToAppleEpisode(ITunesResult x) =>
        new(
            x.TrackId!.Value,
            x.TrackName!,
            x.ReleaseDate?.ToUniversalTime() ?? DateTime.MinValue,
            TimeSpan.FromMilliseconds(x.TrackTimeMillis ?? 0),
            Uri.TryCreate(x.TrackViewUrl, UriKind.Absolute, out var url)
                ? url.CleanAppleUrl()
                : new Uri($"https://podcasts.apple.com/podcast/id{x.CollectionId}?i={x.TrackId}"),
            x.Description ?? string.Empty,
            false);

    private sealed class ITunesResponse
    {
        [JsonPropertyName("results")] public List<ITunesResult> Results { get; set; } = [];
    }

    private sealed class ITunesResult
    {
        [JsonPropertyName("trackCount")] public int? TrackCount { get; set; }
        [JsonPropertyName("collectionId")] public long? CollectionId { get; set; }
        [JsonPropertyName("trackId")] public long? TrackId { get; set; }
        [JsonPropertyName("trackName")] public string? TrackName { get; set; }
        [JsonPropertyName("releaseDate")] public DateTime? ReleaseDate { get; set; }
        [JsonPropertyName("trackTimeMillis")] public long? TrackTimeMillis { get; set; }
        [JsonPropertyName("trackViewUrl")] public string? TrackViewUrl { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }
}
