using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IDataRepository
{
    Task Write<T>(T data) where T : CosmosSelector;
    Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector;
    Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector;
    Task<T?> GetBy<T>(string partitionKey, Func<T, bool> selector) where T : CosmosSelector;
}