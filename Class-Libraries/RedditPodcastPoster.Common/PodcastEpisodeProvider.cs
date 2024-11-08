using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common;

public class PodcastEpisodeProvider(
    IPodcastRepository repository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    ILogger<PodcastEpisodeProvider> logger
) : IPodcastEpisodeProvider
{
    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var numberOfDays = 7;
        var podcastEpisodes = new List<PodcastEpisode>();

        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} init.");
        var untweetedPodcastIds =
            await repository.GetPodcastIdsWithUntweetedReleasedSince(DateTimeExtensions.DaysAgo(numberOfDays));
        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} init.");

        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            logger.LogInformation(
                $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcast)} init.");
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            logger.LogInformation(
                $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcast)} complete.");
            logger.LogInformation(
                $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(podcastEpisodeFilter.GetMostRecentUntweetedEpisodes)} init.");
            var filtered = podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast, youTubeRefreshed, spotifyRefreshed, numberOfDays);
            logger.LogInformation(
                $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(podcastEpisodeFilter.GetMostRecentUntweetedEpisodes)} complete.");
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }
}