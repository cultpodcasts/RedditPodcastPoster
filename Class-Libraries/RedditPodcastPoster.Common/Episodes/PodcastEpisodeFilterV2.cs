using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 implementation that filters episodes from detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodeFilterV2(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IOptions<DelayedYouTubePublication> delayedYouTubePublicationSettings,
    ILogger<PodcastEpisodeFilterV2> logger)
    : IPodcastEpisodeFilterV2
{
    private readonly DelayedYouTubePublication _delayedYouTubePublicationSettings =
        delayedYouTubePublicationSettings.Value;

    public async Task<IEnumerable<PodcastEpisodeV2>> GetNewEpisodesReleasedSince(
        Guid podcastId,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var v2Podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
        if (v2Podcast == null)
        {
            logger.LogWarning("Podcast with id '{PodcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisodeV2>();
        }

        var v2Episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

        var matchingEpisodes = v2Episodes
            .Where(episode => IsReadyToPost(episode, since));

        var resolvedEpisodes = new List<PodcastEpisodeV2>();
        foreach (var matchingEpisode in matchingEpisodes)
        {
            var post = !v2Podcast.IsDelayedYouTubePublishing(matchingEpisode);

            if (post)
            {
                resolvedEpisodes.Add(new PodcastEpisodeV2(v2Podcast, matchingEpisode));
            }
        }

        return resolvedEpisodes.Where(x =>
            EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed));
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentUntweetedEpisodes(
        Guid podcastId,
        int numberOfDays)
    {
        var v2Podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
        if (v2Podcast == null)
        {
            logger.LogWarning("Podcast with id '{PodcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisodeV2>();
        }

        var v2Episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = v2Episodes
            .Where(e =>
                e.Release >= since &&
                e is { Removed: false, Ignored: false, Tweeted: false } &&
                (e.Urls.YouTube != null || e.Urls.Spotify != null))
            .Select(e => new PodcastEpisodeV2(v2Podcast, e))
            .Where(pe => !v2Podcast.IsDelayedYouTubePublishing(pe.Episode))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();

        if (!podcastEpisodes.Any())
        {
            logger.LogInformation(
                "No Podcast-Episode found ready to tweet for podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                v2Podcast.Name, podcastId);
        }

        return podcastEpisodes;
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetMostRecentBlueskyReadyEpisodes(
        Guid podcastId,
        int numberOfDays)
    {
        var v2Podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
        if (v2Podcast == null)
        {
            logger.LogWarning("Podcast with id '{PodcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisodeV2>();
        }

        var v2Episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = v2Episodes
            .Where(e =>
                e.Release >= since &&
                e is { Removed: false, Ignored: false } &&
                (!e.BlueskyPosted.HasValue || !e.BlueskyPosted.Value) &&
                (e.Urls.YouTube != null || e.Urls.Spotify != null))
            .Select(e => new PodcastEpisodeV2(v2Podcast, e))
            .Where(pe => !v2Podcast.IsDelayedYouTubePublishing(pe.Episode))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();

        if (!podcastEpisodes.Any())
        {
            logger.LogInformation(
                "No Podcast-Episode found ready for Bluesky for podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                v2Podcast.Name, podcastId);
        }

        return podcastEpisodes;
    }

    public bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode)
    {
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay > TimeSpan.Zero)
        {
            var evaluationThreshold = _delayedYouTubePublicationSettings.EvaluationThreshold;
            if (episode.Length > TimeSpan.Zero)
            {
                evaluationThreshold += episode.Length;
            }

            var timeSinceRelease = DateTime.UtcNow - episode.Release;
            var thresholdElapsed = timeSinceRelease > youTubePublishingDelay;
            var withinEvaluationWindow = timeSinceRelease - youTubePublishingDelay < evaluationThreshold;
            return thresholdElapsed && withinEvaluationWindow;
        }

        return false;
    }

    private static bool IsReadyToPost(Models.V2.Episode episode, DateTime since)
    {
        return
            episode.Release >= since &&
            episode is { Removed: false, Ignored: false, Posted: false } &&
            (episode.Urls.YouTube != null || episode.Urls.Spotify != null);
    }

    private static bool EliminateItemsDueToIndexingErrors(
        PodcastEpisodeV2 podcastEpisode,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var eliminateForYouTube =
            podcastEpisode.Podcast.ReleaseAuthority == Service.YouTube &&
            !youTubeRefreshed &&
            podcastEpisode.Episode.Urls.YouTube == null;

        var eliminateForSpotify =
            podcastEpisode.Podcast.ReleaseAuthority == Service.Spotify &&
            !spotifyRefreshed &&
            podcastEpisode.Episode.Urls.Spotify == null;

        return !(eliminateForYouTube || eliminateForSpotify);
    }
}
