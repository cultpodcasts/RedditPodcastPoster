using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
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

        // Create legacy for filtering logic (temporary until refactored)
        var legacyPodcast = ToLegacyPodcast(v2Podcast, v2Episodes.Select(ToLegacyEpisode).ToList());
        var legacyEpisodes = v2Episodes.Select(ToLegacyEpisode).ToList();

        var matchingEpisodes = legacyEpisodes
            .Where(episode => IsReadyToPost(legacyPodcast, episode, since));

        var resolvedEpisodes = new List<PodcastEpisodeV2>();
        foreach (var matchingEpisode in matchingEpisodes)
        {
            var post = !legacyPodcast.IsDelayedYouTubePublishing(matchingEpisode);

            if (post)
            {
                var v2Episode = v2Episodes.First(e => e.Id == matchingEpisode.Id);
                resolvedEpisodes.Add(new PodcastEpisodeV2(v2Podcast, v2Episode));
            }
        }

        return resolvedEpisodes.Where(x =>
            EliminateItemsDueToIndexingErrors(x.ToLegacy(), youTubeRefreshed, spotifyRefreshed));
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
        var legacyPodcast = ToLegacyPodcast(v2Podcast, v2Episodes.Select(ToLegacyEpisode).ToList());

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = v2Episodes
            .Where(e =>
                e.Release >= since &&
                e is { Removed: false, Ignored: false, Tweeted: false } &&
                (e.Urls.YouTube != null || e.Urls.Spotify != null))
            .Select(e => new PodcastEpisodeV2(v2Podcast, e))
            .Where(pe => !legacyPodcast.IsDelayedYouTubePublishing(pe.Episode.ToLegacyEpisode()))
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
        var legacyPodcast = ToLegacyPodcast(v2Podcast, v2Episodes.Select(ToLegacyEpisode).ToList());

        var since = DateTimeExtensions.DaysAgo(numberOfDays);
        var podcastEpisodes = v2Episodes
            .Where(e =>
                e.Release >= since &&
                e is { Removed: false, Ignored: false } &&
                (!e.BlueskyPosted.HasValue || !e.BlueskyPosted.Value) &&
                (e.Urls.YouTube != null || e.Urls.Spotify != null))
            .Select(e => new PodcastEpisodeV2(v2Podcast, e))
            .Where(pe => !legacyPodcast.IsDelayedYouTubePublishing(pe.Episode.ToLegacyEpisode()))
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

    private bool IsReadyToPost(Podcast podcast, Episode episode, DateTime since)
    {
        return
            episode.Release >= since &&
            episode is { Removed: false, Ignored: false, Posted: false } &&
            (episode.Urls.YouTube != null || episode.Urls.Spotify != null);
    }

    private bool EliminateItemsDueToIndexingErrors(
        PodcastEpisode podcastEpisode,
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

    private static Episode ToLegacyEpisode(Models.V2.Episode v2Episode)
    {
        return new Episode
        {
            Id = v2Episode.Id,
            Title = v2Episode.Title,
            Description = v2Episode.Description,
            Release = v2Episode.Release,
            Length = v2Episode.Length,
            Explicit = v2Episode.Explicit,
            Posted = v2Episode.Posted,
            Tweeted = v2Episode.Tweeted,
            BlueskyPosted = v2Episode.BlueskyPosted,
            Ignored = v2Episode.Ignored,
            Removed = v2Episode.Removed,
            SpotifyId = v2Episode.SpotifyId,
            AppleId = v2Episode.AppleId,
            YouTubeId = v2Episode.YouTubeId,
            Urls = v2Episode.Urls,
            Subjects = v2Episode.Subjects,
            SearchTerms = v2Episode.SearchTerms,
            Language = v2Episode.Language,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }

    private static Podcast ToLegacyPodcast(Models.V2.Podcast v2Podcast, List<Episode> episodes)
    {
        return new Podcast(v2Podcast.Id)
        {
            Name = v2Podcast.Name,
            Language = v2Podcast.Language,
            Removed = v2Podcast.Removed,
            Publisher = v2Podcast.Publisher,
            Bundles = v2Podcast.Bundles,
            IndexAllEpisodes = v2Podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = v2Podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = v2Podcast.BypassShortEpisodeChecking,
            MinimumDuration = v2Podcast.MinimumDuration,
            ReleaseAuthority = v2Podcast.ReleaseAuthority,
            PrimaryPostService = v2Podcast.PrimaryPostService,
            SpotifyId = v2Podcast.SpotifyId,
            SpotifyMarket = v2Podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = v2Podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = v2Podcast.AppleId,
            YouTubeChannelId = v2Podcast.YouTubeChannelId,
            YouTubePlaylistId = v2Podcast.YouTubePlaylistId,
            YouTubePublicationOffset = v2Podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = v2Podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = v2Podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = v2Podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = v2Podcast.TwitterHandle,
            BlueskyHandle = v2Podcast.BlueskyHandle,
            HashTag = v2Podcast.HashTag,
            EnrichmentHashTags = v2Podcast.EnrichmentHashTags,
            TitleRegex = v2Podcast.TitleRegex,
            DescriptionRegex = v2Podcast.DescriptionRegex,
            EpisodeMatchRegex = v2Podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = v2Podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = v2Podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = v2Podcast.IgnoredSubjects,
            DefaultSubject = v2Podcast.DefaultSubject,
            SearchTerms = v2Podcast.SearchTerms,
            KnownTerms = v2Podcast.KnownTerms,
            FileKey = v2Podcast.FileKey,
            Timestamp = v2Podcast.Timestamp,
            Episodes = episodes
        };
    }
}
