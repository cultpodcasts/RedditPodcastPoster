using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class DiscoveryResultProcessor(
    IUrlCategoriser urlCategoriser,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ICategorisedItemProcessor categorisedItemProcessor,
    ILogger<DiscoveryResultProcessor> logger) : IDiscoveryResultProcessor
{
    public async Task<SubmitResult> CreateSubmitResult(DiscoveryResult discoveryResult, IndexingContext indexingContext,
        SubmitOptions submitOptions, Podcast? spotifyPodcast, Podcast? applePodcast, Podcast? youTubePodcast)
    {
        var result = await CreateCategorisedItem(discoveryResult, indexingContext, submitOptions,
            spotifyPodcast, applePodcast, youTubePodcast);
        var categorisedItem = result.CategorisedItem;
        if (result.EnrichSpotify)
        {
            categorisedItem = await ExtractSpotify(result.CategorisedItem, discoveryResult.Urls, indexingContext);
        }

        if (result.EnrichApple)
        {
            categorisedItem = await ExtractApple(categorisedItem, discoveryResult.Urls, indexingContext);
        }

        if (result.EnrichYouTube)
        {
            categorisedItem = await ExtractYouTube(categorisedItem, discoveryResult.Urls, indexingContext);
        }

        var submitResult = await categorisedItemProcessor.ProcessCategorisedItem(categorisedItem, submitOptions);
        return submitResult;
    }

    private async Task<CreateCategorisedItemResponse> CreateCategorisedItem(DiscoveryResult discoveryResult,
        IndexingContext indexingContext,
        SubmitOptions submitOptions, Podcast? spotifyPodcast, Podcast? applePodcast, Podcast? youTubePodcast)
    {
        bool enrichSpotify = false, enrichApple = false, enrichYouTube = false;
        CategorisedItem categorisedItem;
        if (discoveryResult.Urls.Spotify != null &&
            (spotifyPodcast != null || (applePodcast == null && youTubePodcast == null)))
        {
            categorisedItem = await urlCategoriser.Categorise(spotifyPodcast, discoveryResult.Urls.Spotify,
                indexingContext, submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.Apple != null)
            {
                enrichApple = true;
            }

            if (discoveryResult.Urls.YouTube != null)
            {
                enrichYouTube = true;
            }
        }
        else if (discoveryResult.Urls.Apple != null &&
                 (applePodcast != null || (spotifyPodcast == null && youTubePodcast == null)))
        {
            categorisedItem = await urlCategoriser.Categorise(applePodcast, discoveryResult.Urls.Apple, indexingContext,
                submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.YouTube != null)
            {
                enrichYouTube = true;
            }

            if (discoveryResult.Urls.Spotify != null)
            {
                enrichSpotify = true;
            }
        }
        else
        {
            categorisedItem = await urlCategoriser.Categorise(youTubePodcast, discoveryResult.Urls.YouTube!,
                indexingContext, submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.Apple != null)
            {
                enrichApple = true;
            }

            if (discoveryResult.Urls.Spotify != null)
            {
                enrichSpotify = true;
            }
        }

        return new CreateCategorisedItemResponse(categorisedItem, enrichSpotify, enrichApple, enrichYouTube);
    }

    private async Task<CategorisedItem> ExtractSpotify(
        CategorisedItem categorisedItem,
        DiscoveryResultUrls urls,
        IndexingContext indexingContext)
    {
        if (categorisedItem.ResolvedSpotifyItem == null ||
            categorisedItem.ResolvedSpotifyItem.EpisodeDescription !=
            SpotifyIdResolver.GetEpisodeId(urls.Spotify!))
        {
            categorisedItem = categorisedItem with
            {
                ResolvedSpotifyItem =
                await spotifyUrlCategoriser.Resolve(null, urls.Spotify!, indexingContext)
            };
        }

        return categorisedItem;
    }

    private async Task<CategorisedItem> ExtractApple(CategorisedItem categorisedItem, DiscoveryResultUrls urls,
        IndexingContext indexingContext)
    {
        if (categorisedItem.ResolvedAppleItem == null ||
            categorisedItem.ResolvedAppleItem.ShowId !=
            AppleIdResolver.GetPodcastId(urls.Apple!) ||
            categorisedItem.ResolvedAppleItem.EpisodeId !=
            AppleIdResolver.GetEpisodeId(urls.Apple!))
        {
            categorisedItem = categorisedItem with
            {
                ResolvedAppleItem =
                await appleUrlCategoriser.Resolve(null, urls.Apple!, indexingContext)
            };
        }

        return categorisedItem;
    }

    private async Task<CategorisedItem> ExtractYouTube(CategorisedItem categorisedItem, DiscoveryResultUrls urls,
        IndexingContext indexingContext)
    {
        if (categorisedItem.ResolvedYouTubeItem == null ||
            categorisedItem.ResolvedYouTubeItem.ShowId != YouTubeIdResolver.Extract(urls.YouTube!))
        {
            categorisedItem = categorisedItem with
            {
                ResolvedYouTubeItem = await youTubeUrlCategoriser.Resolve(null, urls.YouTube!, indexingContext)
            };
        }

        return categorisedItem;
    }
}