namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Interface for lazily-initialized async instances that are cached after first access.
/// The singleton lifetime is controlled by the DI container, not this interface.
/// This simply defers async initialization until first use and caches the result.
/// </summary>
/// <typeparam name="T">The type of instance being cached</typeparam>
public interface IAsyncInstance<T>
{
    /// <summary>
    /// Gets the instance. On first call, triggers async initialization.
    /// Subsequent calls return the cached result.
    /// </summary>
    Task<T> GetAsync();
}
