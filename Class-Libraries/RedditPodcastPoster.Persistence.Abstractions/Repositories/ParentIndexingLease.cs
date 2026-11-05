using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Holds a parent indexing marker and renews its heartbeat until disposed. Dispose releases the marker (If-Match).
/// </summary>
public sealed class ParentIndexingLease : IAsyncDisposable
{
    private readonly IParentIndexingLockStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _heartbeatInterval;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _heartbeatCts = new();
    private readonly Task _heartbeat;
    private ParentIndexingLockHandle _handle;
    private int _disposed;

    private ParentIndexingLease(
        IParentIndexingLockStore store,
        TimeProvider timeProvider,
        ParentIndexingLockHandle handle,
        TimeSpan heartbeatInterval,
        ILogger? logger)
    {
        _store = store;
        _timeProvider = timeProvider;
        _handle = handle;
        _heartbeatInterval = heartbeatInterval;
        _logger = logger;
        _heartbeat = heartbeatInterval > TimeSpan.Zero
            ? RunHeartbeat()
            : Task.CompletedTask;
    }

    public ParentIndexingLockHandle Handle => _handle;

    /// <summary>
    /// Acquires the marker and starts heartbeats. Returns null when another index holds a fresh marker
    /// (<see cref="ParentIndexingLockOutcome.AlreadyRunning"/>).
    /// </summary>
    public static async Task<ParentIndexingLease?> TryStart(
        IParentIndexingLockStore store,
        TimeProvider timeProvider,
        Guid parentId,
        TimeSpan staleAfter,
        TimeSpan heartbeatInterval,
        string? source,
        ILogger? logger = null)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var acquisition = await store.TryAcquire(parentId, now, staleAfter, source);
        if (acquisition.Outcome == ParentIndexingLockOutcome.AlreadyRunning || acquisition.Handle is null)
        {
            return null;
        }

        return new ParentIndexingLease(store, timeProvider, acquisition.Handle, heartbeatInterval, logger);
    }

    private async Task RunHeartbeat()
    {
        try
        {
            while (!_heartbeatCts.IsCancellationRequested)
            {
                // Wall-clock delay (not TimeProvider): fake-time advances used for per-parent budgets must not
                // fire heartbeats, and staleness in production is measured in real elapsed time anyway.
                await Task.Delay(_heartbeatInterval, _heartbeatCts.Token);
                var renewed = await _store.Renew(_handle, _timeProvider.GetUtcNow().UtcDateTime);
                if (renewed is null)
                {
                    _logger?.LogWarning(
                        "Parent indexing lease lost heartbeat for parent '{parentId}'.",
                        _handle.ParentId);
                    return;
                }

                _handle = renewed;
            }
        }
        catch (OperationCanceledException) when (_heartbeatCts.IsCancellationRequested)
        {
            // Released.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _heartbeatCts.CancelAsync();
        // Do not await the heartbeat task: cancel is enough, and awaiting a TimeProvider-driven or long delay
        // can hang budget tests that race dispose with fake-time advances. Release tolerates a lost etag.
        await _store.Release(_handle);
        _heartbeatCts.Dispose();
    }
}
