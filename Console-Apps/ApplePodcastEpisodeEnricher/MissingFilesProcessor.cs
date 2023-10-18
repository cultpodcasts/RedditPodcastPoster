using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace ApplePodcastEpisodeEnricher;

public class MissingFilesProcessor
{
    private readonly IApplePodcastService _applePodcastService;
    private readonly IEpisodeMatcher _episodeMatcher;
    private readonly ILogger<MissingFilesProcessor> _logger;
    private readonly IPodcastRepository _podcastsRepository;

    public MissingFilesProcessor(
        IApplePodcastService applePodcastService,
        IPodcastRepository podcastsRepository,
        IEpisodeMatcher episodeMatcher,
        ILogger<MissingFilesProcessor> logger)
    {
        _applePodcastService = applePodcastService;
        _podcastsRepository = podcastsRepository;
        _episodeMatcher = episodeMatcher;
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

            var episodes = podcast.Episodes.Where(x => x.AppleId == null || x.Urls.Apple == null);
            if (episodes.Any() && podcast.AppleId.HasValue)
            {
                var appleApiRecords =
                    await _applePodcastService.GetEpisodes(new ApplePodcastId(podcast.AppleId.Value),
                        new IndexingContext());
                if (appleApiRecords == null)
                {
                    throw new InvalidOperationException(
                        $"Could not retrieve episodes for podcast '{podcast.Name}' with id '{podcast.Id}'.");
                }

                foreach (var episode in episodes)
                {
                    var matchingApiRecord = appleApiRecords.SingleOrDefault(appleEpisode =>
                        _episodeMatcher.IsMatch(episode,
                            new Episode
                            {
                                Title = appleEpisode.Title, Release = appleEpisode.Release,
                                Length = appleEpisode.Duration
                            }, episodeMatchRegex));
                    if (matchingApiRecord != null)
                    {
                        if (episode.AppleId == null)
                        {
                            episode.AppleId = matchingApiRecord.Id;
                        }

                        if (episode.Urls.Apple == null)
                        {
                            episode.Urls.Apple = matchingApiRecord.Url;
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
}