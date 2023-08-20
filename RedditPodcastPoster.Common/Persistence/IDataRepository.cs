namespace RedditPodcastPoster.Common.Persistence;

public interface IDataRepository
{
    IKeySelector KeySelector { get; }
    Task Write<T>(string key, T data);
    Task<T?> Read<T>(string key) where T : class;
    IAsyncEnumerable<T> GetAll<T>() where T : class;
}