using System.Linq.Expressions;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeRepository
{
    Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId, Expression<Func<Episode, bool>> selector);
    Task<Episode?> GetMostRecentByPodcastId(Guid podcastId);
    Task Save(Episode episode);
    Task Save(IEnumerable<Episode> episodes);
    Task Delete(Guid podcastId, Guid episodeId);
    Task<Episode?> GetBy(Expression<Func<Episode, bool>> selector);
    IAsyncEnumerable<Episode> GetAllBy(Expression<Func<Episode, bool>> selector);

    IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Episode, bool>> selector,
        Expression<Func<Episode, TProjection>> projection);
}