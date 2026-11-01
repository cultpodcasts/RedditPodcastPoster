using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// The submit-side catalogue ownership rule, shared by <see cref="Submitters.UrlSubmitter"/> and
/// <see cref="Submitters.CatalogueOwnedDiscoveryUrlSubmitter"/>. Same ownership lookup as the indexer
/// (<see cref="IUrlMembershipLookup.FindCatalogueServiceUrlOwner"/>), not gated by submit content types.
/// </summary>
public class CatalogueOwnershipGuard(IUrlMembershipLookup urlMembershipLookup) : ICatalogueOwnershipGuard
{
    public Task<CatalogueServiceUrlOwner?> FindOwner(Uri url, CancellationToken cancellationToken) =>
        EpisodeUrlRouter.IsPodcastPlatformUrl(url)
            ? urlMembershipLookup.FindCatalogueServiceUrlOwner(url, cancellationToken)
            : Task.FromResult<CatalogueServiceUrlOwner?>(null);

    /// <summary>
    /// True when an owned URL must stop as already-exists instead of being submitted under <paramref name="parent"/>:
    /// <list type="bullet">
    /// <item>a film (or film trailer) owns it, because films have no podcast-platform resubmit path;</item>
    /// <item>no TV show / news organisation was routed or targeted, so it would become a podcast episode;</item>
    /// <item>the routed or targeted parent is not the owner's, because the parent submitter only matches items within
    /// that parent and would create a second item.</item>
    /// </list>
    /// A TV episode / news report re-submitted under its own parent continues, so the parent submitter can enrich it.
    /// </summary>
    public static bool Blocks(CatalogueServiceUrlOwner owner, CatalogueParentMatch? parent) =>
        owner.ContentKind == SubmitClassification.Film ||
        parent == null ||
        parent.ContentKind != owner.ContentKind ||
        owner.ParentId != parent.Parent.Id;
}
