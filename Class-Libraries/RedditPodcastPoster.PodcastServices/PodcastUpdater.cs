using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastUpdater(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeMerger episodeMerger,
    IEpisodeProvider episodeProvider,
    IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IOptions<PostingCriteria> postingCriteria,
    IYouTubeQuotaUsageTracker youTubeQuotaUsageTracker,
    ILogger<PodcastUpdater> logger)
    : IPodcastUpdater
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var knownYouTubeExpensiveQuery = podcast.HasExpensiveYouTubePlaylistQuery();
        var knownYouTubeChannelSearchForbidden = podcast.HasYouTubeChannelSearchForbidden();
        var knownSpotifyExpensiveQuery = podcast.HasExpensiveSpotifyEpisodesQuery();
        IList<Episode> episodes;
        EpisodeMergeResult mergeResult;
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();

        if (!indexingContext.ReleasedSince.HasValue)
        {
            throw new InvalidOperationException($"Cannot index podcast with id '{podcast.Id}' without a released-since");
        }

        var releasedSince = indexingContext.ReleasedSince.Value;
        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            releasedSince += youTubePublishingDelay;
        }

        var repositoryReleasedSince = youTubePublishingDelay < TimeSpan.Zero
            ? releasedSince
            : releasedSince - youTubePublishingDelay;

        logger.LogInformation("'{method}': Podcast '{podcastName}' {nameOfEnrichOnly}= '{enrichOnly}'.", 
            nameof(Update), podcast.Name, nameof(enrichOnly), enrichOnly);
        if (!enrichOnly)
        {
            var releaseScopedEpisodes = await episodeRepository
                .GetByPodcastId(podcast.Id, x => x.Release >= repositoryReleasedSince)
                .ToListAsync();

            var newEpisodes = await episodeProvider.GetEpisodes(podcast, releaseScopedEpisodes, indexingContext);
            var checkShortEpisodes =
                !(podcast.BypassShortEpisodeChecking.HasValue && podcast.BypassShortEpisodeChecking.Value);
            logger.LogInformation("Podcast '{podcastName}' has checkShortEpisodes= '{checkShortEpisodes}'.",
                podcast.Name, checkShortEpisodes);
            if (checkShortEpisodes)
            {
                foreach (var newEpisode in newEpisodes)
                {
                    newEpisode.Ignored = newEpisode.Length < (podcast.MinimumDuration ?? _postingCriteria.MinimumDuration);
                }

                logger.LogInformation("Podcast '{podcastName}' has SkipShortEpisodes= '{SkipShortEpisodes}'.",
                    podcast.Name, indexingContext.SkipShortEpisodes);
                if (indexingContext.SkipShortEpisodes)
                {
                    RemoveIgnoredEpisodes(newEpisodes);
                }
            }

            episodes = await IncludePlatformIdentifiedEpisodesForMerge(podcast.Id, releaseScopedEpisodes);
            mergeResult = episodeMerger.MergeEpisodes(podcast, episodes, newEpisodes);

            // Merge does not mutate `episodes`; new adds live only on mergeResult until saved. Include them
            // before enrichment and elimination filtering so new episodes are not evaluated only on a later index pass.
            episodes = episodes
                .Concat(mergeResult.AddedEpisodes)
                .Where(x => EpisodeInIndexingScope(x, podcast, youTubePublishingDelay, releasedSince))
                .ToList();
        }
        else
        {
            episodes = await episodeRepository
                .GetByPodcastId(podcast.Id, x => x.Release >= repositoryReleasedSince)
                .ToListAsync();

            episodes = episodes
                .Where(x => EpisodeInIndexingScope(x, podcast, youTubePublishingDelay, releasedSince))
                .Where(episode =>
                    (!string.IsNullOrWhiteSpace(podcast.SpotifyId) && string.IsNullOrWhiteSpace(episode.SpotifyId)) ||
                    (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                     string.IsNullOrWhiteSpace(episode.YouTubeId)) ||
                    (podcast.AppleId.HasValue && !episode.AppleId.HasValue)
                ).ToList();
            mergeResult = EpisodeMergeResult.Empty;
        }

        if (podcast.HasIgnoreAllEpisodes())
        {
            foreach (var episode in mergeResult.AddedEpisodes)
            {
                episode.Ignored = true;
            }
        }

        var episodesWithAssignedPlatformIds = await episodeRepository
            .GetByPodcastId(
                podcast.Id,
                x => (x.AppleId != null && x.AppleId > 0) ||
                     (x.SpotifyId != null && x.SpotifyId != string.Empty))
            .ToListAsync();
        var enrichmentEpisodeContext =
            BuildEnrichmentEpisodeContext(episodes, episodesWithAssignedPlatformIds);

        var enrichmentResult = await podcastServicesEpisodeEnricher.EnrichEpisodes(
            podcast, enrichmentEpisodeContext, episodes, indexingContext);
        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, episodes, eliminationTerms.Terms);

        // Persist enriched episodes
        if (enrichmentResult.UpdatedEpisodes.Any())
        {
            await episodeRepository.Save(enrichmentResult.UpdatedEpisodes.Select(x => x.Episode));
        }

        // Persist filtered episodes (marked as removed)
        if (filterResult.FilteredEpisodes.Any())
        {
            await episodeRepository.Save(filterResult.FilteredEpisodes.Select(x => x.Episode));
        }

        // Persist merged episodes
        if (mergeResult.MergedEpisodes.Any())
        {
            // Save the in-place merged existing episodes
            await episodeRepository.Save(mergeResult.MergedEpisodes.Select(x => x.Existing));
        }
        
        // Persist newly added episodes
        if (mergeResult.AddedEpisodes.Any())
        {
            foreach (var added in mergeResult.AddedEpisodes)
            {
                var service = EpisodeCreationLogger.ResolveCreatingService(added, podcast.ReleaseAuthority);
                EpisodeCreationLogger.LogCreated(
                    logger,
                    added,
                    podcast.Id,
                    EpisodeCreationSource.Indexer,
                    service,
                    caller: "PodcastUpdater.Update");
            }

            await episodeRepository.Save(mergeResult.AddedEpisodes);
        }

        var discoveredYouTubeExpensiveQuery = !knownYouTubeExpensiveQuery && podcast.HasExpensiveYouTubePlaylistQuery();
        if (discoveredYouTubeExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive YouTube Query found processing '{podcastName}' with id '{podcast.}' and youtube-channel-id '{podcastYouTubeChannelId}'.",
                podcast.Name, podcast.Id, podcast.YouTubeChannelId);
        }

        var discoveredYouTubeChannelSearchForbidden = !knownYouTubeChannelSearchForbidden &&
                                                      podcast.HasYouTubeChannelSearchForbidden();
        if (discoveredYouTubeChannelSearchForbidden)
        {
            logger.LogInformation(
                "YouTube channel Search.List forbidden for podcast '{podcastName}' with id '{podcastId}' and youtube-channel-id '{podcastYouTubeChannelId}'; persisting uploads-playlist strategy.",
                podcast.Name, podcast.Id, podcast.YouTubeChannelId);
        }

        var discoveredSpotifyExpensiveQuery = !knownSpotifyExpensiveQuery && podcast.HasExpensiveSpotifyEpisodesQuery();
        if (discoveredSpotifyExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive Spotify Query found processing '{podcastName}' with id '{podcastId}' and spotify-id '{podcastSpotifyId}'.",
                podcast.Name, podcast.Id, podcast.SpotifyId);
        }

        var spotifyBypassed = initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving;
        var youTubeBypassed = initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving;
        var youTubeDiscoveryBypassed = podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);
        if (youTubeDiscoveryBypassed)
        {
            logger.LogInformation(
                "YouTube episode discovery bypassed for podcast '{podcastName}' with id '{podcastId}'; LastIndexed will not be updated.",
                podcast.Name, podcast.Id);
        }

        var indexSucceeded = !spotifyBypassed && !youTubeBypassed && !youTubeDiscoveryBypassed &&
                             !mergeResult.FailedEpisodes.Any();

        var podcastChanged = mergeResult.MergedEpisodes.Any() || mergeResult.AddedEpisodes.Any() ||
                             filterResult.FilteredEpisodes.Any() || enrichmentResult.UpdatedEpisodes.Any() ||
                             discoveredYouTubeExpensiveQuery || discoveredYouTubeChannelSearchForbidden ||
                             discoveredSpotifyExpensiveQuery;

        if (indexSucceeded)
        {
            podcast.LastIndexed = DateTime.UtcNow;
            podcastChanged = true;
        }

        if (podcastChanged)
        {
            // Update LatestReleased if new episodes were added or merged
            if (mergeResult.AddedEpisodes.Any())
            {
                var mostRecentAdded = mergeResult.AddedEpisodes.Max(x => x.Release);
                if (podcast.LatestReleased == null || mostRecentAdded > podcast.LatestReleased)
                {
                    podcast.LatestReleased = mostRecentAdded;
                }
            }

            if (mergeResult.MergedEpisodes.Any())
            {
                var mostRecentMerged = mergeResult.MergedEpisodes.Max(x => x.Existing.Release);
                if (podcast.LatestReleased == null || mostRecentMerged > podcast.LatestReleased)
                {
                    podcast.LatestReleased = mostRecentMerged;
                }
            }

            await podcastRepository.Save(podcast);
        }

        await RecordPodcastQuotaImpactIfNeeded(
            podcast,
            enrichOnly,
            indexingContext,
            initialSkipYouTube);

        return new IndexPodcastResult(
            podcast,
            mergeResult,
            filterResult,
            enrichmentResult,
            spotifyBypassed,
            youTubeBypassed);
    }

    private async Task RecordPodcastQuotaImpactIfNeeded(
        Podcast podcast,
        bool enrichOnly,
        IndexingContext indexingContext,
        bool initialSkipYouTube)
    {
        if (initialSkipYouTube || !indexingContext.YouTubeQuotaExhausted)
        {
            return;
        }

        if (podcast.SkipEnrichingFromYouTube == true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
            string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
        {
            return;
        }

        if (enrichOnly || !podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext))
        {
            await youTubeQuotaUsageTracker.RecordPodcastNotEnrichedDueToQuotaAsync();
            return;
        }

        await youTubeQuotaUsageTracker.RecordPodcastNotIndexedDueToQuotaAsync();
    }

    private async Task<IList<Episode>> IncludePlatformIdentifiedEpisodesForMerge(
        Guid podcastId,
        IList<Episode> releaseScopedEpisodes)
    {
        var platformIdentifiedEpisodes = await episodeRepository
            .GetByPodcastId(
                podcastId,
                x => (x.AppleId != null && x.AppleId > 0) ||
                     (x.SpotifyId != null && x.SpotifyId != string.Empty) ||
                     (x.YouTubeId != null && x.YouTubeId != string.Empty) ||
                     x.Urls.Spotify != null)
            .ToListAsync();

        if (platformIdentifiedEpisodes.Count == 0)
        {
            return releaseScopedEpisodes;
        }

        var knownIds = releaseScopedEpisodes.Select(x => x.Id).ToHashSet();
        return releaseScopedEpisodes
            .Concat(platformIdentifiedEpisodes.Where(x => !knownIds.Contains(x.Id)))
            .ToList();
    }

    private static IList<Episode> BuildEnrichmentEpisodeContext(
        IList<Episode> episodesToEnrich,
        IList<Episode> episodesWithAssignedPlatformIds)
    {
        var assignedById = episodesWithAssignedPlatformIds.ToDictionary(x => x.Id);
        var context = episodesToEnrich
            .Select(episode => assignedById.TryGetValue(episode.Id, out var assigned) ? assigned : episode)
            .ToList();

        var indexedIds = episodesToEnrich.Select(x => x.Id).ToHashSet();
        foreach (var assignedEpisode in episodesWithAssignedPlatformIds)
        {
            if (!indexedIds.Contains(assignedEpisode.Id))
            {
                context.Add(assignedEpisode);
            }
        }

        return context;
    }

    private static bool EpisodeInIndexingScope(
        Episode episode,
        Podcast podcast,
        TimeSpan youTubePublishingDelay,
        DateTime releasedSince)
    {
        if (EpisodeReleaseTolerance.ShouldEnrichDespiteReleaseWindow(episode, podcast))
        {
            return ReduceToSinceIncorporatingPublishDelay(episode, youTubePublishingDelay, DateTime.MinValue);
        }

        return ReduceToSinceIncorporatingPublishDelay(episode, youTubePublishingDelay, releasedSince);
    }

    private static bool ReduceToSinceIncorporatingPublishDelay(
        Episode episode,
        TimeSpan youTubePublishingDelay,
        DateTime releasedSince)
    {
        var cutoff = episode.Release + youTubePublishingDelay;
        if (youTubePublishingDelay < TimeSpan.Zero)
        {
            var hasReleasedOnYouTube = DateTime.UtcNow >= cutoff;
            return episode.Release >= releasedSince && hasReleasedOnYouTube;
        }

        var inTimeframe = cutoff > releasedSince;
        return inTimeframe;
    }

    private void RemoveIgnoredEpisodes(IList<Episode> episodes)
    {
        var shortEpisodes = new List<Episode>();

        foreach (var newEpisode in episodes)
        {
            if (newEpisode.Ignored)
            {
                shortEpisodes.Add(newEpisode);
            }
        }

        foreach (var shortEpisode in shortEpisodes)
        {
            logger.LogInformation("Removing short-episode '{episodeTitle}'.", shortEpisode.Title);
            episodes.Remove(shortEpisode);
        }
    }
}
