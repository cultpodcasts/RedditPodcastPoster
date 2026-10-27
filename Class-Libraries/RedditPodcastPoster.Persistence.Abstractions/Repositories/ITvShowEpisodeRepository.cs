using System.Linq.Expressions;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ITvShowEpisodeRepository : IRepository<TvShowEpisode>, IFilterableRepository<TvShowEpisode>
{
    Task<TvShowEpisode?> GetTvShowEpisode(Guid tvShowId, Guid episodeId);
    Task<int> Count(Guid tvShowId);
    IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId);
    IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId, Expression<Func<TvShowEpisode, bool>> selector);
    Task Save(IEnumerable<TvShowEpisode> episodes);
    Task Delete(Guid tvShowId, Guid episodeId);

    /// <summary>Point read that also returns the ETag; null when the episode does not exist.</summary>
    Task<ETagged<TvShowEpisode>?> GetTvShowEpisodeWithETag(Guid tvShowId, Guid episodeId);

    /// <summary>
    /// Cosmos partial patch of <c>/services</c> and/or <c>/youTubeId</c> only, guarded by <paramref name="ifMatchETag"/>.
    /// A missing document is <see cref="CataloguePlayablePatchOutcome.NotFound"/> (never recreated); a changed one is
    /// <see cref="CataloguePlayablePatchOutcome.Conflict"/>.
    /// </summary>
    Task<CataloguePlayablePatchOutcome> PatchFields(
        Guid tvShowId,
        Guid episodeId,
        string ifMatchETag,
        CataloguePlayableFieldPatch patch);
}
