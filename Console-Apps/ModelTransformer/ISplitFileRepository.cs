using RedditPodcastPoster.Models;

namespace ModelTransformer;

public interface ISplitFileRepository
{
    Task Write<T>(string key, T data);
    Task<T?> Read<T>(string key, string partitionKey) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>(string partitionKey) where T : CosmosSelector;
}