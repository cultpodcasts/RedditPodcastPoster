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
        logger.LogInformation("Exec {method}, {execMethod} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            nameof(repository.GetPodcastIdsWithUntweetedReleasedSince),
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            repository.GetPodcastIdsWithUntweetedReleasedSince,
            podcastEpisodeFilter.GetMostRecentUntweetedEpisodes,
            youTubeRefreshed,
            spotifyRefreshed);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            podcastId,
            podcastEpisodeFilter.GetMostRecentUntweetedEpisodes);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method}, {execMethod} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            nameof(repository.GetPodcastIdsWithBlueskyReadyReleasedSince),
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            repository.GetPodcastIdsWithBlueskyReadyReleasedSince,
            podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes,
            youTubeRefreshed,
            spotifyRefreshed);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            podcastId,
            podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes);
    }

    private async Task<IEnumerable<PodcastEpisode>> GetPodcastEpisodes(
        Func<DateTime, Task<IEnumerable<Guid>>> findPodcast,
        Func<Podcast, bool, bool, int, IEnumerable<PodcastEpisode>> filterEpisodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var podcastEpisodes = new List<PodcastEpisode>();
        var dateTime = DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays);

        var untweetedPodcastIds = await findPodcast(dateTime);

        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            if (podcast == null)
            {
                logger.LogError("Podcast with id '{UntweetedPodcastId}' not found.", untweetedPodcastId);
            }
            else
            {
                var filtered = filterEpisodes(podcast, youTubeRefreshed, spotifyRefreshed, _postingCriteria.TweetDays);
                podcastEpisodes.AddRange(filtered);
            }
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private async Task<IEnumerable<PodcastEpisode>> GetPodcastEpisodes(
        Guid podcastId,
        Func<Podcast, int, IEnumerable<PodcastEpisode>> filterEpisodes)
    {
        var podcastEpisodes = new List<PodcastEpisode>();
        var podcast = await repository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{UntweetedPodcastId}' not found.", podcastId);
        }
        else
        {
            var filtered = filterEpisodes(podcast, _postingCriteria.TweetDays);
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }
}