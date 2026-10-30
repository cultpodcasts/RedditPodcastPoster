using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Updaters;

/// <summary>
/// Hourly index pass for TvShow and NewsOrganisation parents migrated from podcasts: finds new
/// playables with the podcast discovery pipeline and saves them as TvShowEpisode / NewsReport.
/// Film has no parent and is never indexed.
/// </summary>
public interface ICatalogueParentsUpdater
{
    /// <summary>Ids of non-removed TV and News parents whose <c>indexing</c> settings auto-index, in id order.</summary>
    Task<Guid[]> GetIndexableParentIds();

    /// <summary>Index these parent ids (TV or News). Unknown or non-indexable ids are skipped. False when any parent failed.</summary>
    Task<bool> UpdateParents(Guid[] parentIds, IndexingContext indexingContext);

    /// <summary>
    /// Index one TV show or news organisation now (curator "Index" on the hub page). <paramref name="parent"/> is the
    /// <c>TvShow</c> or <c>NewsOrganisation</c> the caller already resolved, so the kind the route asked for is the kind
    /// indexed and it is not read again; any other type throws <see cref="ArgumentException"/>. Same discovery, merge,
    /// filters and indexer-state patch as <see cref="UpdateParents"/>; unlike it, a non-indexable parent is reported in
    /// the result instead of skipped, the saved playables are returned so the caller can update search, and the run
    /// has its own bounded on-demand budget (<c>PodcastUpdateBudgetOptions.OnDemandBudget</c>) that fits inside an HTTP
    /// request. The budget's own token is used: it is not linked to the request's token, so a client disconnect does
    /// not stop the work (the budget bounds it).
    /// </summary>
    Task<CatalogueParentIndexResult> IndexParent(Publisher parent, IndexingContext indexingContext);
}
