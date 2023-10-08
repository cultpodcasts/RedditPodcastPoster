using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public interface IDataRepository
{
    IPartitionKeySelector PartitionKeySelector { get; }
    Task Write<T>(string key, T data);
    Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector;
    Task<IEnumerable<Guid>> GetAllIds<T>(string partitionKey) where T : CosmosSelector;
}