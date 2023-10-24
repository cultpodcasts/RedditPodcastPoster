using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IRepository<T> where T : CosmosSelector
{
    Task<IEnumerable<T>> GetAll(string partitionKey);
    Task<T?> Get(string key, string partitionKey);
    Task Save(T data);
}