using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission;

public class DiscoveryUrlSubmitter(
    IPodcastService podcastService,
    IDiscoveryResultProcessor discoveryResultProcessor,
    ISubmitResultAdaptor submitResultAdaptor,
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
            spotifyPodcast = await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Spotify, indexingContext);
        }

        if (discoveryResult.Urls.Apple != null)
        {
            applePodcast = await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Apple, indexingContext);
        }

        if (discoveryResult.Urls.YouTube != null)
        {
            youTubePodcast = await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.YouTube, indexingContext);
        }

        Podcast?[] podcasts = [spotifyPodcast, applePodcast, youTubePodcast];
        IEnumerable<Podcast> foundPodcasts = podcasts.Where(x => x != null)!;
        var areSame = foundPodcasts.All(x => x.Id == foundPodcasts.First().Id);
        if (!areSame)
        {
            return new DiscoverySubmitResult(DiscoverySubmitResultState.DifferentPodcasts);
        }

        var submitResult = await discoveryResultProcessor.CreateSubmitResult(discoveryResult, indexingContext,
            submitOptions, spotifyPodcast, applePodcast, youTubePodcast);


        var state = submitResultAdaptor.ToDiscoverySubmitResultState(submitResult);
        return new DiscoverySubmitResult(state, submitResult.EpisodeId);
    }
}