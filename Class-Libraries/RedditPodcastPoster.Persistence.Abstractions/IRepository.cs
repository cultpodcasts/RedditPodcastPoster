using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IRepository<T> where T : CosmosSelector
{
    Task<IEnumerable<T>> GetAll();
    Task<T?> Get(string key);
    Task Save(T data);
}