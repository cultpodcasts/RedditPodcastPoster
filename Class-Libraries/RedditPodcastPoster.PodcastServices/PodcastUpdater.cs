﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastUpdater(
    IPodcastRepository podcastRepository,
    IEpisodeProvider episodeProvider,
    IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
    IPodcastFilter podcastFilter,
    IEliminationTermsProvider eliminationTermsProvider,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastUpdater> logger)
    : IPodcastUpdater
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var knownYouTubeExpensiveQuery = podcast.HasExpensiveYouTubePlaylistQuery();
        var knownSpotifyExpensiveQuery = podcast.HasExpensiveSpotifyEpisodesQuery();
        IList<Episode> episodes;
        MergeResult mergeResult;
        if (!enrichOnly)
        {
            var newEpisodes = await episodeProvider.GetEpisodes(
                podcast,
                indexingContext);
            if (!(podcast.BypassShortEpisodeChecking.HasValue && podcast.BypassShortEpisodeChecking.Value))
            {
                foreach (var newEpisode in newEpisodes)
                {
                    newEpisode.Ignored = newEpisode.Length < _postingCriteria.MinimumDuration;
                }

                if (indexingContext.SkipShortEpisodes)
                {
                    EliminateShortEpisodes(newEpisodes);
                }
            }

            mergeResult = podcastRepository.Merge(podcast, newEpisodes);
            episodes = podcast.Episodes;

            if (indexingContext.ReleasedSince.HasValue)
            {
                var releasedSince = indexingContext.ReleasedSince.Value;
                var youTubePublishingDelay = podcast.YouTubePublishingDelay();
                if (podcast.ReleaseAuthority == Service.YouTube)
                {
                    releasedSince += youTubePublishingDelay;
                }

                episodes = episodes
                    .Where(x => ReduceToSinceIncorporatingPublishDelay(x, youTubePublishingDelay, releasedSince))
                    .ToList();
            }
        }
        else
        {
            if (!indexingContext.ReleasedSince.HasValue)
            {
                throw new InvalidOperationException(
                    $"Cannot enrich podcast with id '{podcast.Id}' without a released-since");
            }

            var releasedSince = indexingContext.ReleasedSince.Value;
            var youTubePublishingDelay = podcast.YouTubePublishingDelay();
            if (podcast.ReleaseAuthority == Service.YouTube)
            {
                releasedSince += youTubePublishingDelay;
            }

            episodes = podcast.Episodes
                .Where(x => ReduceToSinceIncorporatingPublishDelay(x, youTubePublishingDelay, releasedSince))
                .Where(episode =>
                    (!string.IsNullOrWhiteSpace(podcast.SpotifyId) && string.IsNullOrWhiteSpace(episode.SpotifyId)) ||
                    (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                     string.IsNullOrWhiteSpace(episode.YouTubeId)) ||
                    (podcast.AppleId.HasValue && !episode.AppleId.HasValue)
                ).ToList();
            mergeResult = MergeResult.Empty;
        }

        if (podcast.HasIgnoreAllEpisodes())
        {
            foreach (var episode in episodes)
            {
                episode.Ignored = true;
            }
        }

        var enrichmentResult = await podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, indexingContext);
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, eliminationTerms.Terms);

        var discoveredYouTubeExpensiveQuery = !knownYouTubeExpensiveQuery && podcast.HasExpensiveYouTubePlaylistQuery();
        if (discoveredYouTubeExpensiveQuery)
        {
            logger.LogInformation(
                $"Expensive YouTube Query found processing '{podcast.Name}' with id '{podcast.Id}' and youtube-channel-id '{podcast.YouTubeChannelId}'.");
        }

        var discoveredSpotifyExpensiveQuery = !knownSpotifyExpensiveQuery && podcast.HasExpensiveSpotifyEpisodesQuery();
        if (discoveredSpotifyExpensiveQuery)
        {
            logger.LogInformation(
                $"Expensive Spotify Query found processing '{podcast.Name}' with id '{podcast.Id}' and spotify-id '{podcast.SpotifyId}'.");
        }

        if (mergeResult.MergedEpisodes.Any() || mergeResult.AddedEpisodes.Any() ||
            filterResult.FilteredEpisodes.Any() || enrichmentResult.UpdatedEpisodes.Any() ||
            discoveredYouTubeExpensiveQuery || discoveredSpotifyExpensiveQuery)
        {
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

    private bool ReduceToSinceIncorporatingPublishDelay(Episode episode, TimeSpan youTubePublishingDelay,
        DateTime releasedSince)
    {
        if (youTubePublishingDelay < TimeSpan.Zero)
        {
            var cutoff = episode.Release + youTubePublishingDelay;
            var hasReleasedOnYouTube = DateTime.UtcNow >= cutoff;
            return episode.Release >= releasedSince && hasReleasedOnYouTube;
        }

        var inTimeframe = episode.Release >= releasedSince &&
                          episode.Release - DateTime.UtcNow < youTubePublishingDelay;
        return inTimeframe;
    }

    private void EliminateShortEpisodes(IList<Episode> episodes)
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
            episodes.Remove(shortEpisode);
        }
    }
}