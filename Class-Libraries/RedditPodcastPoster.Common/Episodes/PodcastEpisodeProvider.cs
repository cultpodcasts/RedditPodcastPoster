using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Implementation that provides podcast episodes from detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodeProvider(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
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
        var releasedSince = GetReleasedSince(_postingCriteria.TweetDays);
        return GetReadyPodcastEpisodes(
            nameof(GetUntweetedPodcastEpisodes),
            releasedSince,
            x => !x.Episode.Tweeted,
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
            .Where(x => x.Release >= GetReleasedSince(_postingCriteria.TweetDays) &&
                        x is { Tweeted: false, Ignored: false, Removed: false })
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
        var releasedSince = GetReleasedSince(_postingCriteria.BlueSkyDays);
        return GetReadyPodcastEpisodes(
            nameof(GetBlueskyReadyPodcastEpisodes),
            releasedSince,
            _ => true,
            (podcast, episodes) => podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
                podcast,
                episodes,
                youTubeRefreshed,
                spotifyRefreshed,
                _postingCriteria.BlueSkyDays));
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Bluesky-days: '{blueskyDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.BlueSkyDays);

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{podcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisode>();
        }

        var episodes = await episodeRepository.GetByPodcastId(podcastId)
            .Where(x => x.Release >= GetReleasedSince(_postingCriteria.BlueSkyDays) &&
                        x is { Ignored: false, Removed: false })
            .ToArrayAsync();

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
            podcast,
            episodes,
            _postingCriteria.BlueSkyDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private async Task<IEnumerable<PodcastEpisode>> GetReadyPodcastEpisodes(
        string methodName,
        DateTime releasedSince,
        Func<PodcastEpisode, bool> candidateFilter,
        Func<Podcast, IEnumerable<Episode>, Task<IEnumerable<PodcastEpisode>>> getReadyEpisodes)
    {
        logger.LogInformation("Exec {method} init. Released-since: '{releasedSince:O}'",
            methodName,
            releasedSince);

        var sharedRecentCandidateThreshold = GetReleasedSince(_postingCriteria.MaxDays);

        var candidateEpisodes =
            (await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(sharedRecentCandidateThreshold))
            .Where(x => x.Episode.Release >= releasedSince)
            .Where(candidateFilter)
            .ToArray();

        if (!candidateEpisodes.Any())
        {
            logger.LogWarning(
                "No candidate episodes found for {method}. Released-since: '{releasedSince:u}'.",
                methodName,
                releasedSince);
        }

        var totalCandidateEpisodeCount = candidateEpisodes.Length;
        var removedPodcastCandidateCount = 0;
        var filteredOutEpisodeCount = 0;

        var podcastEpisodes = new List<PodcastEpisode>();
        foreach (var podcastGroup in candidateEpisodes.GroupBy(x => x.Podcast.Id))
        {
            var podcast = podcastGroup.First().Podcast;

            var groupEpisodes = podcastGroup.Select(x => x.Episode).ToArray();
            if (podcast.Removed == true)
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

            podcastEpisodes.AddRange(filtered);
        }

        logger.LogInformation(
            "{method} candidate summary. Total-candidates: {totalCandidates}, removed-podcast-candidates: {removedPodcastCandidates}, filtered-out-candidates: {filteredOutCandidates}, ready-candidates: {readyCandidates}.",
            methodName,
            totalCandidateEpisodeCount,
            removedPodcastCandidateCount,
            filteredOutEpisodeCount,
            podcastEpisodes.Count);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private DateTime GetReleasedSince(int days)
    {
        return DateOnly
            .FromDateTime(DateTime.UtcNow)
            .AddDays(days * -1)
            .ToDateTime(TimeOnly.MinValue);
    }
}