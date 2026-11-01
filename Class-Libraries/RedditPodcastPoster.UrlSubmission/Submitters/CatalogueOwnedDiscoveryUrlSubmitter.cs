using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;

namespace RedditPodcastPoster.UrlSubmission.Submitters;

/// <summary>
/// Applies the toolbar's catalogue ownership rule (<see cref="CatalogueOwnershipGuard"/>) to discovery submits
/// (curator submit, supervisor apply and their language probes) before <see cref="DiscoveryUrlSubmitter"/> runs.
/// When a film, film trailer, TV episode or news report already stores one of the result's URLs and the result would
/// not be filed under that item's own parent, it is <see cref="DiscoverySubmitResultState.EpisodeAlreadyExists"/>
/// and nothing is created (#1089).
/// </summary>
public class CatalogueOwnedDiscoveryUrlSubmitter(
    IDiscoveryUrlSubmitter inner,
    ICatalogueOwnershipGuard catalogueOwnershipGuard,
    IEpisodeUrlRouter episodeUrlRouter,
    ILogger<CatalogueOwnedDiscoveryUrlSubmitter> logger
) : IDiscoveryUrlSubmitter
{
    public async Task<DiscoverySubmitResult> Submit(
        DiscoveryResult discoveryResult,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        Uri?[] urls = [discoveryResult.Urls.Spotify, discoveryResult.Urls.Apple, discoveryResult.Urls.YouTube];
        foreach (var url in urls.OfType<Uri>())
        {
            var owner = await catalogueOwnershipGuard.FindOwner(url, CancellationToken.None);
            if (owner == null)
            {
                continue;
            }

            // Only a TV episode / news report can continue, and only under its own parent: route just this URL.
            var parent = owner.ContentKind == SubmitClassification.Film
                ? null
                : (await episodeUrlRouter.RouteByOwner(url, indexingContext)).Parent;
            if (CatalogueOwnershipGuard.Blocks(owner, parent))
            {
                logger.LogInformation(
                    "Discovery-result '{DiscoveryResultId}' skipped: '{Url}' is already in the catalogue as {ContentKind} '{PlayableId}'.",
                    discoveryResult.Id, url, owner.ContentKind, owner.PlayableId);
                return new DiscoverySubmitResult(
                    DiscoverySubmitResultState.EpisodeAlreadyExists,
                    ContentKind: owner.ContentKind,
                    PlayableId: owner.PlayableId,
                    ParentId: owner.ParentId);
            }
        }

        return await inner.Submit(discoveryResult, indexingContext, submitOptions);
    }
}
