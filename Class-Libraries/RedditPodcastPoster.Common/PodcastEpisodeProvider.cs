using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common;

public class PodcastEpisodeProvider(
    IPodcastRepository repository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodeProvider> logger
) : IPodcastEpisodeProvider
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var podcastEpisodes = new List<PodcastEpisode>();

        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} init. Tweet-days: ${_postingCriteria.TweetDays}");
        var untweetedPodcastIds =
            await repository.GetPodcastIdsWithUntweetedReleasedSince(
                DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays));
        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} complete. Podcasts with untweeted episodes: '{untweetedPodcastIds.Count()}'.");

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
                podcast, youTubeRefreshed, spotifyRefreshed, _postingCriteria.TweetDays);
            logger.LogInformation(
                $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(podcastEpisodeFilter.GetMostRecentUntweetedEpisodes)} complete.");
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }
}