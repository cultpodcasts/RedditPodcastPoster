using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Implementation that provides podcast episodes from detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodeProvider(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodeProvider> logger
) : IPodcastEpisodeProvider
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var releasedSince = GetReleasedSince();
        return GetReadyPodcastEpisodes(
            nameof(GetUntweetedPodcastEpisodes),
            x => x.Release >= releasedSince &&
                 !x.Tweeted &&
                 !x.Ignored &&
                 !x.Removed,
            podcast => podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast,
                youTubeRefreshed,
                spotifyRefreshed,
                _postingCriteria.TweetDays));
    }

    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{podcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisode>();
        }

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
            podcast,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var releasedSince = GetReleasedSince();
        return GetReadyPodcastEpisodes(
            nameof(GetBlueskyReadyPodcastEpisodes),
            x => x.Release >= releasedSince &&
                 x.BlueskyPosted != true &&
                 !x.Ignored &&
                 !x.Removed,
            podcast => podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
                podcast,
                youTubeRefreshed,
                spotifyRefreshed,
                _postingCriteria.TweetDays));
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{podcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisode>();
        }

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
            podcast,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private async Task<IEnumerable<PodcastEpisode>> GetReadyPodcastEpisodes(
        string methodName,
        Expression<Func<Episode, bool>> selector,
        Func<Podcast, Task<IEnumerable<PodcastEpisode>>> getReadyEpisodes)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            methodName,
            _postingCriteria.TweetDays);

        var podcastIds = new HashSet<Guid>(await episodeRepository.GetAllBy(selector)
            .Select(x => x.PodcastId)
            .ToArrayAsync());

        var podcastEpisodes = new List<PodcastEpisode>();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast == null)
            {
                logger.LogError("Podcast with id '{podcastId}' not found.", podcastId);
                continue;
            }

            var filtered = await getReadyEpisodes(podcast);
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private DateTime GetReleasedSince()
    {
        return DateOnly
            .FromDateTime(DateTime.UtcNow)
            .AddDays(_postingCriteria.TweetDays * -1)
            .ToDateTime(TimeOnly.MinValue);
    }
}

