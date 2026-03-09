using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 implementation that submits URLs using detached episode repositories.
/// </summary>
public class UrlSubmitterV2(
    IPodcastRepositoryV2 podcastRepository,
    IPodcastService podcastService,
    IUrlCategoriser urlCategoriser,
    ICategorisedItemProcessorV2 categorisedItemProcessor,
    ILogger<UrlSubmitterV2> logger)
    : IUrlSubmitterV2
{
    public async Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var episodeResult = SubmitResultState.None;
        try
        {
            Podcast? podcast = null;
            if (!submitOptions.CreatePodcast)
            {
                if (submitOptions.PodcastId != null)
                {
                    var v2Podcast = await podcastRepository.GetPodcast(submitOptions.PodcastId.Value);
                    if (v2Podcast != null)
                    {
                        // Convert to legacy for IPodcastService compatibility (temporary)
                        podcast = ToLegacyPodcast(v2Podcast);
                    }
                }
                else
                {
                    podcast = await podcastService.GetPodcastFromEpisodeUrl(url, indexingContext);
                }

                if (podcast != null && podcast.IsRemoved())
                {
                    logger.LogWarning("Podcast with id '{podcastId}' is removed.", podcast.Id);
                    return new SubmitResult(episodeResult, SubmitResultState.PodcastRemoved);
                }
            }

            var categorisedItem =
                await urlCategoriser.Categorise(podcast, url, indexingContext, submitOptions.MatchOtherServices);

            var submitResult = await categorisedItemProcessor.ProcessCategorisedItem(categorisedItem, submitOptions);

            return submitResult;
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Error ingesting '{url}'. Http-request-exception with status: '{status}'", url,
                e.StatusCode);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error ingesting '{url}'.", url);
            return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
        }
    }

    private static Podcast ToLegacyPodcast(V2Podcast v2Podcast)
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
            Episodes = [] // Empty - episodes are detached
        };
    }
}
