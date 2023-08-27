using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;

namespace ApplePodcastEpisodeEnricher;

public class MissingFilesProcessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MissingFilesProcessor> _logger;
    private readonly IPodcastRepository _podcastsRepository;

    public MissingFilesProcessor(
        HttpClient httpClient,
        IPodcastRepository podcastsRepository,
        ILogger<MissingFilesProcessor> logger)
    {
        _httpClient = httpClient;
        _podcastsRepository = podcastsRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _podcastsRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts)
        {
            Regex? episodeMatchRegex = null;
            if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
            {
                episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, RegexOptions.Compiled);
            }


            List<Record> podcastRecords = null;
            var episodes = podcast.Episodes.Where(x => x.AppleId == null || x.Urls.Apple == null);
            if (episodes.Any())
            {
                var appleApiRecords = await GetPodcastRecords(podcast);
                foreach (var episode in episodes)
                {
                    var matchingApiRecord = appleApiRecords.SingleOrDefault(appleEpisode =>
                    {

                        if (episodeMatchRegex == null)
                        {
                            return appleEpisode.Attributes.Name == episode.Title;
                        }

                        var appleEpisodeMatch = episodeMatchRegex.Match(appleEpisode.Attributes.Name);
                        var episodeMatch = episodeMatchRegex.Match(episode.Title);

                        if (appleEpisodeMatch.Groups["episodematch"].Success &&
                            episodeMatch.Groups["episodematch"].Success)
                        {
                            var appleEpisodeUniqueMatch = appleEpisodeMatch.Groups["episodematch"].Value;
                            var episodeUniqueMatch = episodeMatch.Groups["episodematch"].Value;
                            var isMatch = appleEpisodeUniqueMatch == episodeUniqueMatch;
                            return isMatch;
                        }
                        

                        if (appleEpisodeMatch.Groups["title"].Success && episodeMatch.Groups["title"].Success)
                        {
                            var appleEpisodeTitle = appleEpisodeMatch.Groups["title"].Value;
                            var episodeTitle = episodeMatch.Groups["title"].Value;
                            var isMatch = appleEpisodeTitle == episodeTitle;
                            if (isMatch)
                            {
                                return true;
                            }
                        }

                        var publishDifference = episode.Release - appleEpisode.Attributes.Released;
                        if (Math.Abs(publishDifference.Ticks) < TimeSpan.FromMinutes(5).Ticks && Math.Abs(
                                (episode.Length -
                                 appleEpisode.Attributes.Duration).Ticks) < TimeSpan.FromMinutes(1).Ticks)
                        {
                            return true;
                        }

                        return false;
                    });
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
                        _logger.LogInformation($"No matching episode with name '{episode.Title}'.");
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