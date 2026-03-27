using System.Linq.Expressions;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IFilterableRepository<TEntity>
{
    Task<TEntity?> GetBy(Expression<Func<TEntity, bool>> selector);
    IAsyncEnumerable<TEntity> GetAllBy(Expression<Func<TEntity, bool>> selector);
    IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TEntity, bool>> selector,
        Expression<Func<TEntity, TProjection>> projection);
}
