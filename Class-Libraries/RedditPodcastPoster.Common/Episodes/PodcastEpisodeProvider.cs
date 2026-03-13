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
            releasedSince,
            x => x.Release >= releasedSince &&
                 !x.Tweeted &&
                 !x.Ignored &&
                 !x.Removed,
            (podcast, episodes) => podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast,
                episodes,
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

        var episodes = await episodeRepository.GetByPodcastId(podcastId)
            .Where(x => x.Release >= GetReleasedSince() && x is { Tweeted: false, Ignored: false, Removed: false })
            .ToArrayAsync();

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
            podcast,
            episodes,
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
            releasedSince,
            x => x.Release >= releasedSince &&
                 !x.Ignored &&
                 !x.Removed,
            (podcast, episodes) => podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
                podcast,
                episodes,
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

        var episodes = await episodeRepository.GetByPodcastId(podcastId)
            .Where(x => x.Release >= GetReleasedSince() && x is { Ignored: false, Removed: false })
            .ToArrayAsync();

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
            podcast,
            episodes,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private async Task<IEnumerable<PodcastEpisode>> GetReadyPodcastEpisodes(
        string methodName,
        DateTime releasedSince,
        Expression<Func<Episode, bool>> selector,
        Func<Podcast, IEnumerable<Episode>, Task<IEnumerable<PodcastEpisode>>> getReadyEpisodes)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            methodName,
            _postingCriteria.TweetDays);

        var candidateEpisodes = await episodeRepository.GetAllBy(selector).ToArrayAsync();
        if (!candidateEpisodes.Any() && methodName == nameof(GetBlueskyReadyPodcastEpisodes))
        {
            logger.LogWarning(
                "No candidate episodes found for Bluesky posting. Released-since: '{releasedSince:u}', tweet-days: '{tweetDays}'.",
                releasedSince,
                _postingCriteria.TweetDays);
        }

        if (methodName == nameof(GetBlueskyReadyPodcastEpisodes))
        {
            logger.LogWarning(
                "Bluesky candidate episode ids before podcast filtering: {episodeIds}.",
                string.Join(",", candidateEpisodes.Select(x => x.Id)));
        }

        var totalCandidateEpisodeCount = candidateEpisodes.Length;
        var removedPodcastCandidateCount = 0;
        var filteredOutEpisodeCount = 0;

        var podcastEpisodes = new List<PodcastEpisode>();
        foreach (var podcastEpisodeGroup in candidateEpisodes.GroupBy(x => x.PodcastId))
        {
            var podcast = await podcastRepository.GetPodcast(podcastEpisodeGroup.Key);
            if (podcast == null)
            {
                logger.LogError("Podcast with id '{podcastId}' not found.", podcastEpisodeGroup.Key);
                continue;
            }

            var groupEpisodes = podcastEpisodeGroup.ToArray();
            if (methodName == nameof(GetBlueskyReadyPodcastEpisodes) && podcast.Removed == true)
            {
                removedPodcastCandidateCount += groupEpisodes.Length;
                logger.LogWarning(
                    "Skipping candidate episodes because podcast is removed. Podcast '{podcastName}' ({podcastId}), episode-ids: {episodeIds}.",
                    podcast.Name,
                    podcast.Id,
                    string.Join(",", groupEpisodes.Select(x => x.Id)));
            }

            var filtered = (await getReadyEpisodes(podcast, groupEpisodes)).ToArray();
            filteredOutEpisodeCount += Math.Max(0, groupEpisodes.Length - filtered.Length);

            if (!filtered.Any() && methodName == nameof(GetBlueskyReadyPodcastEpisodes))
            {
                logger.LogWarning(
                    "No Bluesky-ready episodes after filtering for podcast '{podcastName}' with id '{podcastId}'. Candidate episode-ids: {episodeIds}.",
                    podcast.Name,
                    podcast.Id,
                    string.Join(",", groupEpisodes.Select(x => x.Id)));
            }

            podcastEpisodes.AddRange(filtered);
        }

        if (methodName == nameof(GetBlueskyReadyPodcastEpisodes))
        {
            logger.LogWarning(
                "Bluesky candidate summary. Total-candidates: {totalCandidates}, removed-podcast-candidates: {removedPodcastCandidates}, filtered-out-candidates: {filteredOutCandidates}, ready-candidates: {readyCandidates}. Ready episode-ids: {readyEpisodeIds}.",
                totalCandidateEpisodeCount,
                removedPodcastCandidateCount,
                filteredOutEpisodeCount,
                podcastEpisodes.Count,
                string.Join(",", podcastEpisodes.Select(x => x.Episode.Id)));
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

