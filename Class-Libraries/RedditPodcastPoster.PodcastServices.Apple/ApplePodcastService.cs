using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastService : IApplePodcastService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplePodcastService> _logger;

    public ApplePodcastService(
        IApplePodcastHttpClientFactory httpClientFactory,
        ILogger<ApplePodcastService> logger)
    {
        _logger = logger;
        _logger.LogInformation($"{nameof(ApplePodcastService)} Creating http-client");
        _httpClient = httpClientFactory.Create().GetAwaiter().GetResult();
    }

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        _logger.LogInformation($"{nameof(GetEpisodes)} podcast-id: '{podcastId}'.");
        var appleEpisodes = await GetEpisodes(podcastId, indexingContext, null);

        return appleEpisodes;
    }

    public async Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext)
    {
        var episodes = await GetEpisodes(podcastId, indexingContext, x => x.Id == episodeId.ToString());
        return episodes?.SingleOrDefault(x => x.Id == episodeId);
    }

    private async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId,
        IndexingContext indexingContext, Func<Record, bool>? breakEvaluator)
    {
        var inDescendingDateOrder = true;
        var requestUri = $"/v1/catalog/us/podcasts/{podcastId.PodcastId}/episodes";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                $"Failed to request '{requestUri}'. Reason: '{ex.Message}', Status-Code: '{ex.StatusCode}'.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to request from '{requestUri}'");
            throw;
        }

        var collectedAppleJson = new List<string>();

        var podcastRecords = new List<Record>();
        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            collectedAppleJson.Add(appleJson);
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            if (appleObject != null && appleObject.Records.Any())
            {
                var lastReleased = appleObject.Records.First().Attributes.Released.Add(TimeSpan.FromSeconds(1));
                foreach (var appleObjectRecord in appleObject.Records)
                {
                    inDescendingDateOrder = lastReleased > appleObjectRecord.Attributes.Released;
                    lastReleased = appleObjectRecord.Attributes.Released;
                    if (!inDescendingDateOrder)
                    {
                        inDescendingDateOrder = false;
                        break;
                    }
                }

                podcastRecords.AddRange(appleObject!.Records);
                while (response.IsSuccessStatusCode &&
                       (breakEvaluator == null || !podcastRecords.Any(breakEvaluator)) &&
                       !string.IsNullOrWhiteSpace(appleObject.Next) &&
                       (
                           !indexingContext.ReleasedSince.HasValue ||
                           podcastRecords.Last().ToAppleEpisode().Release >= indexingContext.ReleasedSince ||
                           !inDescendingDateOrder)
                      )
                {
                    response = await _httpClient.GetAsync((string?) appleObject.Next);
                    if (response.IsSuccessStatusCode)
                    {
                        appleJson = await response.Content.ReadAsStringAsync();
                        collectedAppleJson.Add(appleJson);
                        appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
                        podcastRecords.AddRange(appleObject!.Records);
                    }
                }
            }
        }

        var appleEpisodes = podcastRecords
            .Where(x => x.Attributes.Duration > TimeSpan.Zero)
            .Select(x => x.ToAppleEpisode()).ToArray();
        if (podcastRecords.Any() && !appleEpisodes.Any())
        {
            _logger.LogError(
                $"Missing duration-attribute on all apple-podcast episodes for podcast with apple-podcast-id '{podcastId.PodcastId}'. podcast-records count:'{podcastRecords.Count}',  apple-episodes count:'{appleEpisodes.Count()}'.");
            foreach (var json in collectedAppleJson)
            {
                _logger.LogError(json);
            }
        }
        else
        {
            _logger.LogInformation(
                $"Successfully found podcast-episodes with duration. Apple-podcast-id '{podcastId.PodcastId}', items-with-duration: '{appleEpisodes.Count()}/{podcastRecords.Count}'.");
        }

        return appleEpisodes;
    }
}