namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IRepository<TEntity>
{
    Task Save(TEntity entity);
    Task<int> Count();
    IAsyncEnumerable<TEntity> GetAll();
}
