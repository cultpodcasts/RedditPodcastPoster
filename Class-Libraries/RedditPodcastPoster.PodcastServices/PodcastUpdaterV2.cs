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
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastUpdaterV2(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeMerger episodeMerger,
    IEpisodeProvider episodeProvider,
    IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastUpdaterV2> logger)
    : IPodcastUpdater
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IndexPodcastResult> Update(Podcast podcast, bool enrichOnly, IndexingContext indexingContext)
    {
        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var knownYouTubeExpensiveQuery = podcast.HasExpensiveYouTubePlaylistQuery();
        var knownSpotifyExpensiveQuery = podcast.HasExpensiveSpotifyEpisodesQuery();

        // Get existing episodes from detached repository
        var existingEpisodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();
        var episodes = existingEpisodes.Select(ToLegacyEpisode).ToList();

        EpisodeMergeResult mergeResult;
        MergeResult legacyMergeResult;

        logger.LogInformation("'{method}': Podcast '{podcastName}' {nameOfEnrichOnly}= '{enrichOnly}'.",
            nameof(Update), podcast.Name, nameof(enrichOnly), enrichOnly);

        if (!enrichOnly)
        {
            var newEpisodes = await episodeProvider.GetEpisodes(podcast, indexingContext);
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

            // Use EpisodeMerger to merge episodes
            mergeResult = await episodeMerger.MergeEpisodes(podcast, episodes, newEpisodes);
            legacyMergeResult = new MergeResult(
                mergeResult.AddedEpisodes.ToList(),
                mergeResult.MergedEpisodes.ToList(),
                mergeResult.FailedEpisodes.ToList());

            // Add newly added episodes to the working list
            foreach (var addedEpisode in mergeResult.AddedEpisodes)
            {
                if (!episodes.Any(e => e.Id == addedEpisode.Id))
                {
                    episodes.Add(addedEpisode);
                }
            }

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

            episodes = episodes
                .Where(x => ReduceToSinceIncorporatingPublishDelay(x, youTubePublishingDelay, releasedSince))
                .Where(episode =>
                    (!string.IsNullOrWhiteSpace(podcast.SpotifyId) && string.IsNullOrWhiteSpace(episode.SpotifyId)) ||
                    (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                     string.IsNullOrWhiteSpace(episode.YouTubeId)) ||
                    (podcast.AppleId.HasValue && !episode.AppleId.HasValue)
                ).ToList();
            
            mergeResult = new EpisodeMergeResult([], [], [], []);
            legacyMergeResult = MergeResult.Empty;
        }

        if (podcast.HasIgnoreAllEpisodes())
        {
            foreach (var episode in episodes)
            {
                episode.Ignored = true;
            }
        }

        var enrichmentResult = await podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, indexingContext);
        
        // Convert enriched episodes to V2 and save
        if (enrichmentResult.UpdatedEpisodes.Any())
        {
            var enrichedV2Episodes = enrichmentResult.UpdatedEpisodes
                .Select(er => ToV2Episode(podcast, er.Episode))
                .ToList();
            await episodeRepository.Save(enrichedV2Episodes);
        }

        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        
        // Create a temporary podcast with merged/enriched episodes for filtering
        var tempPodcast = new Podcast(podcast.Id)
        {
            Name = podcast.Name,
            Episodes = episodes.ToList()
        };
        var filterResult = podcastFilter.Filter(tempPodcast, eliminationTerms.Terms);

        var discoveredYouTubeExpensiveQuery = !knownYouTubeExpensiveQuery && podcast.HasExpensiveYouTubePlaylistQuery();
        if (discoveredYouTubeExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive YouTube Query found processing '{podcastName}' with id '{podcastId}' and youtube-channel-id '{podcastYouTubeChannelId}'.",
                podcast.Name, podcast.Id, podcast.YouTubeChannelId);
        }

        var discoveredSpotifyExpensiveQuery = !knownSpotifyExpensiveQuery && podcast.HasExpensiveSpotifyEpisodesQuery();
        if (discoveredSpotifyExpensiveQuery)
        {
            logger.LogInformation(
                "Expensive Spotify Query found processing '{podcastName}' with id '{podcastId}' and spotify-id '{podcastSpotifyId}'.",
                podcast.Name, podcast.Id, podcast.SpotifyId);
        }

        // Save merged episodes to detached repository
        if (mergeResult.EpisodesToSave.Any())
        {
            await episodeRepository.Save(mergeResult.EpisodesToSave);
        }

        // Save podcast metadata updates if needed
        if (legacyMergeResult.MergedEpisodes.Any() || legacyMergeResult.AddedEpisodes.Any() ||
            filterResult.FilteredEpisodes.Any() || enrichmentResult.UpdatedEpisodes.Any() ||
            discoveredYouTubeExpensiveQuery || discoveredSpotifyExpensiveQuery)
        {
            var v2Podcast = ToV2Podcast(podcast);
            await podcastRepository.Save(v2Podcast);
        }

        return new IndexPodcastResult(
            podcast,
            legacyMergeResult,
            filterResult,
            enrichmentResult,
            initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving,
            initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving);
    }

    private bool ReduceToSinceIncorporatingPublishDelay(
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
            Language = v2Episode.SearchLanguage,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }

    private static V2Episode ToV2Episode(Podcast podcast, Episode episode)
    {
        return new V2Episode
        {
            Id = episode.Id,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Removed = episode.Removed,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Subjects = episode.Subjects ?? [],
            SearchTerms = episode.SearchTerms,
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            SearchLanguage = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }

    private static Models.V2.Podcast ToV2Podcast(Podcast legacyPodcast)
    {
        return new Models.V2.Podcast
        {
            Id = legacyPodcast.Id,
            Name = legacyPodcast.Name,
            Language = legacyPodcast.Language,
            Removed = legacyPodcast.Removed,
            Publisher = legacyPodcast.Publisher,
            Bundles = legacyPodcast.Bundles,
            IndexAllEpisodes = legacyPodcast.IndexAllEpisodes,
            IgnoreAllEpisodes = legacyPodcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = legacyPodcast.BypassShortEpisodeChecking,
            MinimumDuration = legacyPodcast.MinimumDuration,
            ReleaseAuthority = legacyPodcast.ReleaseAuthority,
            PrimaryPostService = legacyPodcast.PrimaryPostService,
            SpotifyId = legacyPodcast.SpotifyId,
            SpotifyMarket = legacyPodcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = legacyPodcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = legacyPodcast.AppleId,
            YouTubeChannelId = legacyPodcast.YouTubeChannelId,
            YouTubePlaylistId = legacyPodcast.YouTubePlaylistId,
            YouTubePublicationOffset = legacyPodcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = legacyPodcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = legacyPodcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = legacyPodcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = legacyPodcast.TwitterHandle,
            BlueskyHandle = legacyPodcast.BlueskyHandle,
            HashTag = legacyPodcast.HashTag,
            EnrichmentHashTags = legacyPodcast.EnrichmentHashTags,
            TitleRegex = legacyPodcast.TitleRegex,
            DescriptionRegex = legacyPodcast.DescriptionRegex,
            EpisodeMatchRegex = legacyPodcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = legacyPodcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = legacyPodcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = legacyPodcast.IgnoredSubjects,
            DefaultSubject = legacyPodcast.DefaultSubject,
            SearchTerms = legacyPodcast.SearchTerms,
            KnownTerms = legacyPodcast.KnownTerms,
            FileKey = legacyPodcast.FileKey,
            Timestamp = legacyPodcast.Timestamp
        };
    }
}
