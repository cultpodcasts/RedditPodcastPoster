using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodeFilter(
    IOptions<DelayedYouTubePublication> delayedYouTubePublicationSettings,
    ILogger<PodcastEpisodeFilter> logger)
    : IPodcastEpisodeFilter
{
    private readonly DelayedYouTubePublication _delayedYouTubePublicationSettings =
        delayedYouTubePublicationSettings.Value;

    public Task<IEnumerable<PodcastEpisode>> GetNewEpisodesReleasedSince(
        IEnumerable<PodcastEpisode> podcastEpisodes,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var resolvedPodcastEpisodeSince = podcastEpisodes
            .Where(x => IsReadyToPost(x.Podcast, x.Episode, since))
            .Where(x => !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .Where(x => EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed))
            .ToArray();

        return Task.FromResult<IEnumerable<PodcastEpisode>>(resolvedPodcastEpisodeSince);
    }

    public Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        int numberOfDays)
    {
        if (podcast.Removed == true)
        {
            logger.LogInformation(
                "No Podcast-Episode found ready to tweet for removed podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
            return Task.FromResult(Enumerable.Empty<PodcastEpisode>());
        }

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = episodes
            .Select(e => new PodcastEpisode(podcast, e))
            .Where(x =>
                x.Episode.Release >= since &&
                x.Episode is { Removed: false, Ignored: false, Tweeted: false } &&
                (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();
        if (!podcastEpisodes.Any())
        {
            logger.LogInformation(
                "No Podcast-Episode found ready to tweet for podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
        }

        return Task.FromResult<IEnumerable<PodcastEpisode>>(podcastEpisodes);
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

            var isRecentlyExpiredDelayedPublishing =
                episode.Release.Add(youTubePublishingDelay) <= DateTime.UtcNow &&
                episode.Release.Add(youTubePublishingDelay.Add(evaluationThreshold)) >= DateTime.UtcNow;
            return isRecentlyExpiredDelayedPublishing;
        }

        return false;
    }

    public Task<IEnumerable<PodcastEpisode>> GetMostRecentUntweetedEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays)
    {
        if (podcast.Removed == true)
        {
            logger.LogInformation(
                "No Podcast-Episode found ready to tweet for removed podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
            return Task.FromResult(Enumerable.Empty<PodcastEpisode>());
        }

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = episodes
            .Select(e => new PodcastEpisode(podcast, e))
            .Where(x =>
                x.Episode.Release >= since &&
                x.Episode is { Removed: false, Ignored: false, Tweeted: false } &&
                (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .Where(x => EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();
        if (!podcastEpisodes.Any())
        {
            logger.LogInformation(
                "No Podcast-Episode found ready to tweet for podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
        }

        return Task.FromResult<IEnumerable<PodcastEpisode>>(podcastEpisodes);
    }

    public Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        int numberOfDays)
    {
        if (podcast.Removed == true)
        {
            logger.LogWarning(
                "No Podcast-Episode found ready to Bluesky for removed podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
            return Task.FromResult(Enumerable.Empty<PodcastEpisode>());
        }

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var episodeArray = episodes.ToArray();
        var podcastEpisodes = episodeArray
            .Select(e => new PodcastEpisode(podcast, e))
            .Where(x =>
                x.Episode.Release >= since &&
                x.Episode.BlueskyPosted is null or false &&
                x.Episode is { Removed: false, Ignored: false } &&
                (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .Where(x => EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();
        if (!podcastEpisodes.Any())
        {
            logger.LogWarning(
                "No Podcast-Episode found ready to Bluesky for podcast '{PodcastName}' with podcast-id '{PodcastId}'. Candidate-episodes: {candidateCount}, released-since: '{releasedSince:u}', youTubeRefreshed: {youTubeRefreshed}, spotifyRefreshed: {spotifyRefreshed}.",
                podcast.Name, podcast.Id, episodeArray.Length, since, youTubeRefreshed, spotifyRefreshed);
        }

        return Task.FromResult<IEnumerable<PodcastEpisode>>(podcastEpisodes);
    }

    public Task<IEnumerable<PodcastEpisode>> GetMostRecentBlueskyReadyEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        int numberOfDays)
    {
        if (podcast.Removed == true)
        {
            logger.LogWarning(
                "No Podcast-Episode found ready to Bluesky for removed podcast '{PodcastName}' with podcast-id '{PodcastId}'.",
                podcast.Name, podcast.Id);
            return Task.FromResult(Enumerable.Empty<PodcastEpisode>());
        }

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var episodeArray = episodes.ToArray();
        var podcastEpisodes = episodeArray
            .Select(e => new PodcastEpisode(podcast, e))
            .Where(x =>
                x.Episode.Release >= since &&
                x.Episode.BlueskyPosted is null or false &&
                x.Episode is { Removed: false, Ignored: false } &&
                (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();
        if (!podcastEpisodes.Any())
        {
            logger.LogWarning(
                "No Podcast-Episode found ready to Bluesky for podcast '{PodcastName}' with podcast-id '{PodcastId}'. Candidate-episodes: {candidateCount}, released-since: '{releasedSince:u}'.",
                podcast.Name, podcast.Id, episodeArray.Length, since);
        }

        return Task.FromResult<IEnumerable<PodcastEpisode>>(podcastEpisodes);
    }

    private bool IsReadyToPost(Podcast podcast, Episode episode, DateTime since)
    {
        if (episode.Posted || episode.Ignored || episode.Removed)
        {
            return false;
        }

        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            since += youTubePublishingDelay;
        }

        if (episode.Release >= since)
        {
            if ((!string.IsNullOrWhiteSpace(podcast.SpotifyId) && episode.Urls.Spotify != null) ||
                (string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                 podcast.AppleId != null && episode.Urls.Apple != null) || (podcast.AppleId == null &&
                                                                            !string.IsNullOrWhiteSpace(
                                                                                podcast.YouTubeChannelId) &&
                                                                            episode.Urls.YouTube != null) ||
                string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
            {
                return true;
            }
        }

        var releasedSince = episode.Release >= since && episode.Release - DateTime.UtcNow < youTubePublishingDelay;

        return releasedSince || IsRecentlyExpiredDelayedPublishing(podcast, episode);
    }

    private bool EliminateItemsDueToIndexingErrors(
        PodcastEpisode podcastEpisode,
        bool youTubeRefreshed,
        bool _)
    {
        if (!youTubeRefreshed &&
            !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.YouTubeChannelId) &&
            string.IsNullOrWhiteSpace(podcastEpisode.Episode.YouTubeId))
        {
            if (podcastEpisode.Episode.Release.TimeOfDay > TimeSpan.Zero &&
                podcastEpisode.Podcast.YouTubePublishingDelay() >= TimeSpan.Zero &&
                DateTime.UtcNow < podcastEpisode.Episode.Release + podcastEpisode.Podcast.YouTubePublishingDelay())
            {
                logger.LogInformation(
                    "{EliminateItemsDueToIndexingErrorsName} Eliminating episode with episode-id '{EpisodeId}' and episode-title '{EpisodeTitle}' from podcast with podcast-id '{PodcastId}' and podcast-name '{PodcastName}' due to '{YouTubeRefreshedName}'='{YouTubeRefreshed}'.",
                    nameof(EliminateItemsDueToIndexingErrors), podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                    podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name, nameof(youTubeRefreshed), youTubeRefreshed);
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.YouTubeChannelId) &&
            IsRecentlyExpiredDelayedPublishing(podcastEpisode.Podcast, podcastEpisode.Episode) &&
            string.IsNullOrWhiteSpace(podcastEpisode.Episode.YouTubeId))
        {
            logger.LogInformation(
                "{EliminateItemsDueToIndexingErrorsName} Eliminating episode with episode-id '{EpisodeId}' and episode-title '{EpisodeTitle}' from podcast with podcast-id '{PodcastId}' and podcast-name '{PodcastName}' due to Recently-Expired Delayed Publishing.",
                nameof(EliminateItemsDueToIndexingErrors), podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
            return false;
        }

        return true;
    }
}
