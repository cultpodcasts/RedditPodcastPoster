namespace RedditPodcastPoster.Persistence;

public interface IRepository<T> 
{
    Task<IEnumerable<T>> GetAll(string partitionKey);
    Task Save(string key, T data);
}