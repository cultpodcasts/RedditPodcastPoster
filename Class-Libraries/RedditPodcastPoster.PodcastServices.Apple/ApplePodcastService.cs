using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastService : IApplePodcastService
{
    private readonly IApplePodcastHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApplePodcastService> _logger;

    public ApplePodcastService(
        IApplePodcastHttpClientFactory httpClientFactory,
        ILogger<ApplePodcastService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var requestUri = $"/v1/catalog/us/podcasts/{podcastId.PodcastId}/episodes";
        HttpResponseMessage response;
        HttpClient httpClient;
        try
        {
            httpClient = await _httpClientFactory.Create();
            response = await httpClient.GetAsync(requestUri);
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

        var podcastRecords = new List<Record>();
        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            podcastRecords.AddRange(appleObject!.Records);
            while (!string.IsNullOrWhiteSpace(appleObject.Next) &&
                   (!indexingContext.ReleasedSince.HasValue || podcastRecords.Last().ToAppleEpisode().Release >=
                       indexingContext.ReleasedSince))
            {
                response = await httpClient.GetAsync((string?) appleObject.Next);
                if (response.IsSuccessStatusCode)
                {
                    appleJson = await response.Content.ReadAsStringAsync();
                    appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
                    podcastRecords.AddRange(appleObject!.Records);
                }
            }
        }

        return podcastRecords.Select(x => x.ToAppleEpisode());
    }
}