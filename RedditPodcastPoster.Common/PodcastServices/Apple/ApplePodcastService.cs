using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class ApplePodcastService : IApplePodcastService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplePodcastService> _logger;

    public ApplePodcastService(
        HttpClient httpClient,
        ILogger<ApplePodcastService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
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
                response = await _httpClient.GetAsync(appleObject.Next);
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