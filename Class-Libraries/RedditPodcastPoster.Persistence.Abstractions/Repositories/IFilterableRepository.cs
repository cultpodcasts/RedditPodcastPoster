using System.Linq.Expressions;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFilterableRepository<TEntity>
{
    Task<TEntity?> GetBy(Expression<Func<TEntity, bool>> selector);
    IAsyncEnumerable<TEntity> GetAllBy(Expression<Func<TEntity, bool>> selector);

    /// <summary>
    /// Projects matching documents through Cosmos LINQ. The projection must be a
    /// <c>new TProjection { Prop = … }</c> member-init into a type with a parameterless
    /// constructor and settable properties. Constructor invocation
    /// (<c>new TProjection(…)</c> / positional records), anonymous types, and
    /// <see cref="ValueTuple"/> projections are rejected by the Cosmos LINQ translator.
    /// In-memory fakes that <c>.Compile()</c> the expression may accept those shapes —
    /// a green unit test is not proof the projection translates against Cosmos.
    /// </summary>
    IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TEntity, bool>> selector,
        Expression<Func<TEntity, TProjection>> projection);
}
