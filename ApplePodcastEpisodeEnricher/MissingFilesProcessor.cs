using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;

namespace ApplePodcastEpisodeEnricher;

public class MissingFilesProcessor
{
    private readonly IApplePodcastService _applePodcastService;
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly ILogger<MissingFilesProcessor> _logger;
    private readonly IPodcastRepository _podcastsRepository;

    public MissingFilesProcessor(
        IApplePodcastService applePodcastService,
        IAppleEpisodeResolver appleEpisodeResolver,
        IPodcastRepository podcastsRepository,
        ILogger<MissingFilesProcessor> logger)
    {
        _applePodcastService = applePodcastService;
        _appleEpisodeResolver = appleEpisodeResolver;
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

            var episodes = podcast.Episodes.Where(x => x.AppleId == null || x.Urls.Apple == null);
            if (episodes.Any() && podcast.AppleId.HasValue)
            {
                var appleApiRecords = await _applePodcastService.GetEpisodes(podcast.AppleId.Value);
                foreach (var episode in episodes)
                {
                    var matchingApiRecord = appleApiRecords.SingleOrDefault(appleEpisode =>
                    {
                        if (episodeMatchRegex == null)
                        {
                            return appleEpisode.Title == episode.Title;
                        }

                        var appleEpisodeMatch = episodeMatchRegex.Match(appleEpisode.Title);
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

                        var publishDifference = episode.Release - appleEpisode.Release;
                        if (Math.Abs(publishDifference.Ticks) < TimeSpan.FromMinutes(5).Ticks && Math.Abs(
                                (episode.Length -
                                 appleEpisode.Duration).Ticks) < TimeSpan.FromMinutes(1).Ticks)
                        {
                            return true;
                        }

                        return false;
                    });
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