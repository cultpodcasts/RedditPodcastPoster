using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>In-memory single-flight indexing markers with the same fresh / stale / heartbeat rules as the LookUps store.</summary>
public sealed class InMemoryParentIndexingLockStore : IParentIndexingLockStore
{
    private readonly Dictionary<Guid, Marker> _markers = new();

    private sealed record Marker(
        Guid AttemptId,
        DateTime StartedUtc,
        DateTime LastAliveUtc,
        string? Source);

    /// <summary>Heartbeats that renewed a marker, in call order.</summary>
    public List<DateTime> Renewals { get; } = [];

    /// <summary>When set, every renewal throws it (a transient LookUps failure); the marker is left as it was.</summary>
    public Exception? RenewError { get; set; }

    /// <summary>Renewal calls, successful or not.</summary>
    public int RenewAttempts { get; private set; }

    public void SeedMarker(Guid parentId, DateTime startedUtc, string? source = null)
    {
        lock (_markers)
        {
            _markers[parentId] = new Marker(Guid.NewGuid(), startedUtc, startedUtc, source);
        }
    }

    /// <summary>Another attempt takes the marker over, so the current holder's next heartbeat finds it lost.</summary>
    public void StealMarker(Guid parentId, DateTime nowUtc)
    {
        lock (_markers)
        {
            var source = _markers.TryGetValue(parentId, out var existing) ? existing.Source : null;
            _markers[parentId] = new Marker(Guid.NewGuid(), nowUtc, nowUtc, source);
        }
    }

    public bool IsHeld(Guid parentId)
    {
        lock (_markers)
        {
            return _markers.ContainsKey(parentId);
        }
    }

    public Task<ParentIndexingLockAcquisition> TryAcquire(
        Guid parentId,
        DateTime nowUtc,
        TimeSpan staleAfter,
        string? source = null)
    {
        lock (_markers)
        {
            var outcome = ParentIndexingLockOutcome.Acquired;
            DateTime? runningSince = null;
            if (_markers.TryGetValue(parentId, out var existing))
            {
                if (nowUtc - existing.LastAliveUtc < staleAfter)
                {
                    return Task.FromResult(ParentIndexingLockAcquisition.AlreadyRunning(existing.StartedUtc));
                }

                outcome = ParentIndexingLockOutcome.ResumedStale;
                runningSince = existing.StartedUtc;
            }

            var attemptId = Guid.NewGuid();
            _markers[parentId] = new Marker(attemptId, nowUtc, nowUtc, source);
            return Task.FromResult(new ParentIndexingLockAcquisition(
                outcome,
                new ParentIndexingLockHandle(parentId, attemptId, attemptId.ToString(), nowUtc, source),
                runningSince));
        }
    }

    public Task<ParentIndexingLockHandle?> Renew(ParentIndexingLockHandle handle, DateTime nowUtc)
    {
        lock (_markers)
        {
            RenewAttempts++;
            if (RenewError is { } error)
            {
                return Task.FromException<ParentIndexingLockHandle?>(error);
            }

            if (!_markers.TryGetValue(handle.ParentId, out var existing) || existing.AttemptId != handle.AttemptId)
            {
                return Task.FromResult<ParentIndexingLockHandle?>(null);
            }

            _markers[handle.ParentId] = existing with { LastAliveUtc = nowUtc };
            Renewals.Add(nowUtc);
            return Task.FromResult<ParentIndexingLockHandle?>(handle);
        }
    }

    public Task Release(ParentIndexingLockHandle handle)
    {
        lock (_markers)
        {
            if (_markers.TryGetValue(handle.ParentId, out var existing) && existing.AttemptId == handle.AttemptId)
            {
                _markers.Remove(handle.ParentId);
            }
        }

        return Task.CompletedTask;
    }

    public Task<ParentIndexingLock?> TryGet(Guid parentId)
    {
        lock (_markers)
        {
            if (!_markers.TryGetValue(parentId, out var existing))
            {
                return Task.FromResult<ParentIndexingLock?>(null);
            }

            return Task.FromResult<ParentIndexingLock?>(new ParentIndexingLock
            {
                Id = ParentIndexingLock.IdFor(parentId),
                ParentId = parentId,
                StartedUtc = existing.StartedUtc,
                AttemptId = existing.AttemptId,
                HeartbeatUtc = existing.LastAliveUtc == existing.StartedUtc ? null : existing.LastAliveUtc,
                Source = existing.Source
            });
        }
    }
}
