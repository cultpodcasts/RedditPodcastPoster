using System.Text.Json;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;

namespace ApplePodcastEpisodeEnricher;

public class MissingFilesProcessor
{
    private readonly HttpClient _httpClient;
    private readonly IPodcastRepository _podcastsRepository;

    public MissingFilesProcessor(HttpClient httpClient, IPodcastRepository podcastsRepository)
    {
        _httpClient = httpClient;
        _podcastsRepository = podcastsRepository;
    }

    public async Task Run()
    {
        var podcasts = await _podcastsRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts)
        {
            List<Record> podcastRecords = null;
            var episodes = podcast.Episodes.Where(x => x.AppleId == null || x.Urls.Apple == null);
            if (episodes.Any())
            {
                var appleApiRecords = await GetPodcastRecords(podcast);
                foreach (var episode in episodes)
                {
                    var matchingApiRecord = appleApiRecords.SingleOrDefault(x => x.Attributes.Name == episode.Title);
                    if (matchingApiRecord != null)
                    {
                        if (episode.AppleId == null)
                        {
                            episode.AppleId = long.Parse(matchingApiRecord.Id);
                        }

                        if (episode.Urls.Apple == null)
                        {
                            episode.Urls.Apple = new Uri(matchingApiRecord.Attributes.Url, UriKind.Absolute);
                        }
                    }
                    else
                    {
                        Console.Write($"No matching episode with name '{episode.Title}'.");
                    }
                }

                await _podcastsRepository.Save(podcast);
            }
        }

        Console.ReadKey();
    }

    public async Task<List<Record>> GetPodcastRecords(Podcast podcast)
    {
        var response =
            await _httpClient.GetAsync($"/v1/catalog/us/podcasts/{podcast.AppleId}/episodes");
        var podcastRecords = new List<Record>();
        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            podcastRecords.AddRange(appleObject.Records);
            while (!string.IsNullOrWhiteSpace(appleObject.Next))
            {
                response = await _httpClient.GetAsync(appleObject.Next);
                if (response.IsSuccessStatusCode)
                {
                    appleJson = await response.Content.ReadAsStringAsync();
                    appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
                    podcastRecords.AddRange(appleObject.Records);
                }
            }
        }

        return podcastRecords;
    }
}