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
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} init. Tweet-days: '{_postingCriteria.TweetDays}'");
        var untweetedPodcastIds =
            await repository.GetPodcastIdsWithUntweetedReleasedSince(
                DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays));
        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcastIdsWithUntweetedReleasedSince)} complete. Podcasts with untweeted episodes: '{untweetedPodcastIds.Count()}'.");

        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcast)} & {nameof(podcastEpisodeFilter.GetMostRecentUntweetedEpisodes)} init.");
        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            var filtered = podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast, youTubeRefreshed, spotifyRefreshed, _postingCriteria.TweetDays);
            podcastEpisodes.AddRange(filtered);
        }

        logger.LogInformation(
            $"{nameof(PodcastEpisodeProvider)}.{nameof(GetUntweetedPodcastEpisodes)}: Exec {nameof(repository.GetPodcast)} & {nameof(podcastEpisodeFilter.GetMostRecentUntweetedEpisodes)} complete.");

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }
}