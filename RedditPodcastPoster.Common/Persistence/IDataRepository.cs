using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public interface IDataRepository
{
    IKeySelector KeySelector { get; }
    Task Write<T>(string key, T data);
    Task<T?> Read<T>(string key, string partitionKey) where T : class;
    IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector;
    Task<IEnumerable<Guid>> GetAllIds(string key);
}