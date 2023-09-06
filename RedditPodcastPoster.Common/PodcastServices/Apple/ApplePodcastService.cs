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

    public async Task<IEnumerable<AppleEpisode>> GetEpisodes(long podcastId)
    {
        var response =
            await _httpClient.GetAsync($"/v1/catalog/us/podcasts/{podcastId}/episodes");
        var podcastRecords = new List<Record>();
        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            podcastRecords.AddRange(appleObject!.Records);
            while (!string.IsNullOrWhiteSpace(appleObject.Next))
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