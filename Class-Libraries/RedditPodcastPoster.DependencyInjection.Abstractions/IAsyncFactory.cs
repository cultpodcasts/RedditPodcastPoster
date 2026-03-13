namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Generic interface for async factory operations.
/// Used to abstract creation of objects that require async initialization.
/// </summary>
/// <typeparam name="T">The type of object to create</typeparam>
public interface IAsyncFactory<T>
{
    /// <summary>
    /// Asynchronously creates and initializes an instance of T.
    /// </summary>
    Task<T> Create();
}
