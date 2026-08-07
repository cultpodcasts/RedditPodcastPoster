namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Defers async initialization and caches a successful result.
/// Faulted or cancelled attempts are cleared so a later call can retry.
/// </summary>
/// <typeparam name="T">The type being created by the factory</typeparam>
public class AsyncInstance<T>(IAsyncFactory<T> factory) : IAsyncInstance<T>
{
    private readonly object _gate = new();
    private Task<T>? _instance;

    /// <inheritdoc />
    public Task<T> GetAsync(CancellationToken cancellationToken = default)
    {
        var existing = Volatile.Read(ref _instance);
        if (existing is { IsCompletedSuccessfully: true })
        {
            return existing;
        }

        return GetCoreAsync(cancellationToken);
    }

    private async Task<T> GetCoreAsync(CancellationToken cancellationToken)
    {
        Task<T> task;
        lock (_gate)
        {
            var current = _instance;
            if (current is { IsCompletedSuccessfully: true } || current is { IsCompleted: false })
            {
                task = current;
            }
            else
            {
                // null, faulted, or canceled — start a new attempt with this caller's token
                task = factory.Create(cancellationToken);
                _instance = task;
            }
        }

        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                if (ReferenceEquals(_instance, task) && task.IsCompleted && !task.IsCompletedSuccessfully)
                {
                    _instance = null;
                }
            }

            throw;
        }
    }
}
