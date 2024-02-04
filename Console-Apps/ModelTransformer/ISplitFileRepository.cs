using RedditPodcastPoster.Models;

namespace ModelTransformer;

public interface ISplitFileRepository
{
    Task Write<T>(string key, T data) where T : CosmosSelector;
    Task<T?> Read<T>(string key) where T : CosmosSelector;
    IAsyncEnumerable<T> GetAll<T>() where T : CosmosSelector;
}