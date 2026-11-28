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

    /// <summary>
    /// TV show ids whose <c>trailers[].id</c> equals <paramref name="trailerId"/> (RPP #1412).
    /// TV shows are partitioned by their own id, so this reads across partitions.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetTvShowIdsByTrailerId(Guid trailerId);

    /// <summary>The TV show with its Cosmos ETag, for an optimistic <see cref="TryReplace"/>. Null when it does not exist.</summary>
    Task<TvShowVersion?> GetTvShowForUpdate(Guid tvShowId);

    /// <summary>
    /// Replaces the TV show only if its stored ETag still equals <paramref name="etag"/>. False when another write got
    /// there first (or the show is gone), so the caller re-reads and re-applies instead of overwriting that write.
    /// </summary>
    Task<bool> TryReplace(TvShow tvShow, string etag);

    /// <summary>
    /// Creates the TV show only when no document has its id (Cosmos create, not upsert). False when one already does:
    /// the caller re-reads it. Lets concurrent creates of the same deterministic id converge on one document.
    /// </summary>
    Task<bool> TryCreate(TvShow tvShow);
}

/// <summary>A TV show as read, plus the ETag a <see cref="ITvShowRepository.TryReplace"/> must match.</summary>
public sealed record TvShowVersion(TvShow TvShow, string ETag);
