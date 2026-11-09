using System.Linq.Expressions;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFilterableRepository<TEntity>
{
    Task<TEntity?> GetBy(Expression<Func<TEntity, bool>> selector);
    IAsyncEnumerable<TEntity> GetAllBy(Expression<Func<TEntity, bool>> selector);

    /// <summary>
    /// Projects matching documents through Cosmos LINQ. The projection must be a
    /// <c>new TProjection { Prop = … }</c> member-init into a type with a parameterless
    /// constructor and settable properties; constructor invocation
    /// (<c>new TProjection(…)</c> / positional records) is rejected by the Cosmos LINQ
    /// translator. Verified read-only against production Cosmos for #1042.
    /// </summary>
    IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TEntity, bool>> selector,
        Expression<Func<TEntity, TProjection>> projection);
}
