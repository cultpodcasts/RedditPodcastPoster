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
    /// Index one TV show or news organisation now (curator "Index" on the hub page). Same discovery, merge, filters,
    /// time budget and indexer-state patch as <see cref="UpdateParents"/>; unlike it, an unknown or non-indexable id is
    /// reported in the result instead of skipped, and the saved playables are returned so the caller can update search.
    /// </summary>
    Task<CatalogueParentIndexResult> IndexParent(Guid parentId, IndexingContext indexingContext);
}
