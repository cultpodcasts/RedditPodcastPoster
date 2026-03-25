File: Class-Libraries\RedditPodcastPoster.PodcastServices\PodcastUpdater.cs
````````csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastUpdater(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeMerger episodeMerger,
    IEpisodeProvider episodeProvider,
    IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastUpdater> logger)
    : IPodcastUpdater
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IndexPodcastResult> Update(RedditPodcastPoster.Models.V2.Podcast podcast, bool enrichOnly, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var knownYouTubeExpensiveQuery = podcast.HasExpensiveYouTubePlaylistQuery();
        var knownSpotifyExpensiveQuery = podcast.HasExpensiveSpotifyEpisodesQuery();
        IList<Models.V2.Episode> episodes;
        EpisodeMergeResult mergeResult;
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();

        if (!indexingContext.ReleasedSince.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot index podcast with id '{podcast.Id}' without a released-since");
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
            episodes = await episodeRepository
                .GetByPodcastId(podcast.Id, x => x.Release >= repositoryReleasedSince)
                .ToListAsync();

            var newEpisodes = await episodeProvider.GetEpisodes(podcast, episodes, indexingContext);
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
            
            mergeResult = episodeMerger.MergeEpisodes(podcast, episodes, newEpisodes);

            episodes = episodes
                .Where(x => ReduceToSinceIncorporatingPublishDelay(x, youTubePublishingDelay, releasedSince))
                .ToList();
        }
        else
        {
            episodes = await episodeRepository
                .GetByPodcastId(podcast.Id, x => x.Release >= repositoryReleasedSince)
                .ToListAsync();

            episodes = episodes
                .Where(x => ReduceToSinceIncorporatingPublishDelay(x, youTubePublishingDelay, releasedSince))
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

        var enrichmentResult = await podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, episodes, indexingContext);
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
            await episodeRepository.Save(mergeResult.AddedEpisodes);
        }

        var discoveredYouTubeExpensiveQuery = !knownYouTubeExpensiveQuery && podcast.HasExpensiveYouTubePlaylistQuery();
        if (discoveredYouTubeExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive YouTube Query found processing '{podcastName}' with id '{podcast.}' and youtube-channel-id '{podcastYouTubeChannelId}'.",
                podcast.Name, podcast.Id, podcast.YouTubeChannelId);
        }

        var discoveredSpotifyExpensiveQuery = !knownSpotifyExpensiveQuery && podcast.HasExpensiveSpotifyEpisodesQuery();
        if (discoveredSpotifyExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive Spotify Query found processing '{podcastName}' with id '{podcastId}' and spotify-id '{podcastSpotifyId}'.",
                podcast.Name, podcast.Id, podcast.SpotifyId);
        }

        if (mergeResult.MergedEpisodes.Any() || mergeResult.AddedEpisodes.Any() ||
            filterResult.FilteredEpisodes.Any() || enrichmentResult.UpdatedEpisodes.Any() ||
            discoveredYouTubeExpensiveQuery || discoveredSpotifyExpensiveQuery)
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

        return new IndexPodcastResult(
            podcast,
            mergeResult,
            filterResult,
            enrichmentResult,
            initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving,
            initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving);
    }

    private bool ReduceToSinceIncorporatingPublishDelay(
        Models.V2.Episode episode,
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

    private void RemoveIgnoredEpisodes(IList<Models.V2.Episode> episodes)
    {
        var shortEpisodes = new List<Models.V2.Episode>();

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