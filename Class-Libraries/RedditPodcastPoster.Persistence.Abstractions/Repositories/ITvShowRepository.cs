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
}
