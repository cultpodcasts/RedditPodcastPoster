using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Adaptors;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Submitters;

/// <summary>
/// Ingests a discovery result (curator submit and supervisor apply). Each URL is routed with the same channel-owner
/// rule as the toolbar submit (<see cref="IEpisodeUrlRouter"/>): when a news organisation or TV show owns the
/// channel / show, the item is stored under it as a news report / TV episode and no podcast is created.
/// </summary>
public class DiscoveryUrlSubmitter(
    IEpisodeUrlRouter episodeUrlRouter,
    IDiscoveryResultProcessor discoveryResultProcessor,
    ICatalogueParentSubmitter catalogueParentSubmitter,
    ISubmitResultAdaptor submitResultAdaptor,
    ILogger<DiscoveryUrlSubmitter> logger
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

        // Discovery path always tags creates as Discovery regardless of caller-supplied CreationSource.
        submitOptions = submitOptions with { CreationSource = EpisodeCreationSource.Discovery };

        var spotify = await Route(discoveryResult.Urls.Spotify, indexingContext);
        var apple = await Route(discoveryResult.Urls.Apple, indexingContext);
        var youTube = await Route(discoveryResult.Urls.YouTube, indexingContext);
        RoutedUrl?[] routed = [spotify, apple, youTube];
        var present = routed.Where(x => x != null).Select(x => x!).ToArray();

        var parentRoutes = present.Where(x => x.Route.Parent != null).ToArray();
        if (parentRoutes.Length > 0)
        {
            var parentIds = parentRoutes.Select(x => x.Route.Parent!.Parent.Id).Distinct().ToArray();
            var podcastIds = present.Where(x => x.Route.Podcast != null).Select(x => x.Route.Podcast!.Id).Distinct()
                .ToArray();
            if (parentIds.Length > 1 || podcastIds.Length > 0)
            {
                logger.LogWarning(
                    "Discovery-result '{DiscoveryResultId}' urls belong to different owners: news organisations / TV shows {ParentIds}, podcasts {PodcastIds}.",
                    discoveryResult.Id, string.Join(",", parentIds), string.Join(",", podcastIds));
                return new DiscoverySubmitResult(DiscoverySubmitResultState.DifferentPodcasts);
            }

            return await SubmitToParent(discoveryResult, parentRoutes, indexingContext, submitOptions);
        }

        Podcast? spotifyPodcast = spotify?.Route.Podcast,
            applePodcast = apple?.Route.Podcast,
            youTubePodcast = youTube?.Route.Podcast;
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
        return new DiscoverySubmitResult(state, submitResult.Episode);
    }

    private async Task<RoutedUrl?> Route(Uri? url, IndexingContext indexingContext) =>
        url == null ? null : new RoutedUrl(url, await episodeUrlRouter.RouteByOwner(url, indexingContext));

    /// <summary>
    /// Stores the item under the news organisation / TV show through the toolbar's parent submitter: the first owned
    /// URL creates (or matches) the item; any other URL the same parent owns then enriches it. URLs the parent does not
    /// own are left to cross-service matching rather than filed under a channel / show that is not the parent's.
    /// </summary>
    private async Task<DiscoverySubmitResult> SubmitToParent(
        DiscoveryResult discoveryResult,
        IReadOnlyList<RoutedUrl> parentRoutes,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var parent = parentRoutes[0].Route.Parent!;
        if (parent.Parent.IsRemoved())
        {
            throw new InvalidOperationException(
                $"{parent.Kind} '{parent.Parent.Name}' ({parent.Parent.Id}) owns the channel of this discovery result but is removed.");
        }

        logger.LogInformation(
            "Discovery-result '{DiscoveryResultId}' routed to {Kind} '{ParentName}' ({ParentId}); no podcast is created.",
            discoveryResult.Id, parent.Kind, parent.Parent.Name, parent.Parent.Id);

        var result = await catalogueParentSubmitter.Submit(parent, parentRoutes[0].Url, indexingContext, submitOptions);
        var episodeResult = result.EpisodeResult;
        var playableId = result.PlayableId;
        var searchSource = result.CatalogueSearchSource;
        foreach (var other in parentRoutes.Skip(1))
        {
            try
            {
                var enriched = await catalogueParentSubmitter.Submit(parent, other.Url, indexingContext, submitOptions);
                if (enriched.PlayableId != null && playableId != null && enriched.PlayableId != playableId)
                {
                    logger.LogWarning(
                        "Discovery-result '{DiscoveryResultId}': '{Url}' matched {ContentKind} '{OtherPlayableId}', not '{PlayableId}'.",
                        discoveryResult.Id, other.Url, parent.ContentKind, enriched.PlayableId, playableId);
                    continue;
                }

                playableId ??= enriched.PlayableId;
                searchSource = enriched.CatalogueSearchSource ?? searchSource;
                if (enriched.EpisodeResult == SubmitResultState.Enriched &&
                    episodeResult is SubmitResultState.EpisodeAlreadyExists or SubmitResultState.None)
                {
                    episodeResult = SubmitResultState.Enriched;
                }
            }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or ArgumentException)
            {
                // The item is stored: a failed extra link must not mark the result as errored (a retry would be a no-op).
                logger.LogWarning(e,
                    "Discovery-result '{DiscoveryResultId}': could not add '{Url}' to {ContentKind} '{PlayableId}'.",
                    discoveryResult.Id, other.Url, parent.ContentKind, playableId);
            }
        }

        var state = submitResultAdaptor.ToDiscoverySubmitResultState(result with { EpisodeResult = episodeResult });
        return new DiscoverySubmitResult(
            state,
            ContentKind: parent.ContentKind,
            PlayableId: playableId,
            ParentId: parent.Parent.Id,
            CatalogueSearchSource: searchSource);
    }

    private sealed record RoutedUrl(Uri Url, EpisodeUrlRoute Route);
}
