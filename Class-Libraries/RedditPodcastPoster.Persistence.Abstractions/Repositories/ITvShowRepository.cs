using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ITvShowRepository : IRepository<TvShow>, IFilterableRepository<TvShow>
{
    Task<TvShow?> GetTvShow(Guid tvShowId);
    Task Delete(Guid tvShowId);

    /// <summary>
    /// Patch only the Indexer-owned fields (<see cref="CatalogueParentIndexerPatch"/>), so a curator edit,
    /// rename or removal saved during an index pass is not overwritten. Returns false when the TvShow no
    /// longer exists.
    /// </summary>
    Task<bool> PatchIndexerState(Guid tvShowId, CatalogueParentIndexerPatch patch);

    /// <summary>
    /// Raise <c>latestReleased</c> to the given value only when the stored value is missing or older, in one
    /// conditional patch, so a concurrent writer that already raised it is never lowered. A stored value that is
    /// already as new counts as done. Returns false when the TvShow no longer exists.
    /// </summary>
    Task<bool> RaiseLatestReleased(Guid tvShowId, DateTime latestReleased);
}
