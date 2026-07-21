namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IRepository<TEntity>
{
    Task Save(TEntity entity);
    Task<int> Count();
    IAsyncEnumerable<TEntity> GetAll();
}
