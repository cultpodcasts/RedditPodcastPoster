using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class DiscoveryUrlSubmitter(
    IPodcastService podcastService,
    IUrlCategoriser urlCategoriser,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ICategorisedItemProcessor categorisedItemProcessor,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryUrlSubmitter> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IDiscoveryUrlSubmitter
{
    public async Task<DiscoverySubmitResult> Submit(
        DiscoveryResult discoveryResult,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        if (!discoveryResult.Urls.Any())
        {
            return new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls);
        }

        Podcast? spotifyPodcast = null, applePodcast = null, youTubePodcast = null;
        if (discoveryResult.Urls.Spotify != null)
        {
            spotifyPodcast =
                await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Spotify, indexingContext);
        }

        if (discoveryResult.Urls.Apple != null)
        {
            applePodcast = await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Apple, indexingContext);
        }

        if (discoveryResult.Urls.YouTube != null)
        {
            youTubePodcast =
                await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.YouTube, indexingContext);
        }

        Podcast?[] podcasts = [spotifyPodcast, applePodcast, youTubePodcast];
        IEnumerable<Podcast> foundPodcasts = podcasts.Where(x => x != null)!;
        var areSame = foundPodcasts.All(x => x.Id == foundPodcasts.First().Id);
        if (!areSame)
        {
            return new DiscoverySubmitResult(DiscoverySubmitResultState.DifferentPodcasts);
        }

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

        if (enrichSpotify)
        {
            categorisedItem = await ExtractSpotify(categorisedItem, discoveryResult.Urls, indexingContext);
        }

        if (enrichApple)
        {
            categorisedItem = await ExtractApple(categorisedItem, discoveryResult.Urls, indexingContext);
        }

        if (enrichYouTube)
        {
            categorisedItem = await ExtractYouTube(categorisedItem, discoveryResult.Urls, indexingContext);
        }

        var submitResult = await categorisedItemProcessor.ProcessCategorisedItem(categorisedItem, submitOptions);
        var state = CreateState(submitResult);
        return new DiscoverySubmitResult(state, submitResult.EpisodeId);
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

    private static DiscoverySubmitResultState CreateState(SubmitResult submitResult)
    {
        DiscoverySubmitResultState state;
        if (submitResult is
            {
                PodcastResult: SubmitResult.SubmitResultState.Created,
                EpisodeResult: SubmitResult.SubmitResultState.Created
            })
        {
            state = DiscoverySubmitResultState.CreatedPodcastAndEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.Enriched,
                     EpisodeResult: SubmitResult.SubmitResultState.Created
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcastAndCreatedEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.Enriched,
                     EpisodeResult: SubmitResult.SubmitResultState.None
                     or SubmitResult.SubmitResultState.EpisodeAlreadyExists
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcast;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.Enriched,
                     EpisodeResult: SubmitResult.SubmitResultState.Enriched
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcastAndEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.None,
                     EpisodeResult: SubmitResult.SubmitResultState.Created
                 })
        {
            state = DiscoverySubmitResultState.CreatedEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.None,
                     EpisodeResult: SubmitResult.SubmitResultState.EpisodeAlreadyExists
                 })
        {
            state = DiscoverySubmitResultState.EpisodeAlreadyExists;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResult.SubmitResultState.None,
                     EpisodeResult: SubmitResult.SubmitResultState.Enriched
                 })
        {
            state = DiscoverySubmitResultState.EnrichedEpisode;
        }
        else
        {
            throw new ArgumentException(
                $"Unknown state: podcast-result: '{submitResult.PodcastResult.ToString()}', episode-result '{submitResult.EpisodeResult.ToString()}'.");
        }

        return state;
    }
}