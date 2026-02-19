namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Generic wrapper that defers async initialization using Lazy<Task<T>>.
/// Caches the result after first access. The singleton lifetime is controlled by the DI container.
/// </summary>
/// <typeparam name="T">The type being created by the factory</typeparam>
public class AsyncInstance<T>(IAsyncFactory<T> factory) : IAsyncInstance<T>
{
    private readonly Lazy<Task<T>> _instance = new(factory.Create);

    /// <summary>
    /// Gets the instance. On first call, triggers async initialization.
    /// Subsequent calls return the cached result.
    /// </summary>
    public Task<T> GetAsync() => _instance.Value;
}
