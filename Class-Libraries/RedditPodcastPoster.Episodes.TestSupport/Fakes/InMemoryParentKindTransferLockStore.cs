using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>In-memory single-flight markers with the same fresh / stale / heartbeat rules as the LookUps store.</summary>
public sealed class InMemoryParentKindTransferLockStore : IParentKindTransferLockStore
{
    private readonly Dictionary<Guid, Marker> _markers = new();

    private sealed record Marker(Guid AttemptId, CatalogueParentKind TargetKind, DateTime StartedUtc, DateTime LastAliveUtc);

    /// <summary>Heartbeats that renewed a marker, in call order.</summary>
    public List<DateTime> Renewals { get; } = [];

    /// <summary>When set, every renewal throws it (a transient LookUps failure); the marker is left as it was.</summary>
    public Exception? RenewError { get; set; }

    /// <summary>Renewal calls, successful or not.</summary>
    public int RenewAttempts { get; private set; }

    public void SeedMarker(Guid podcastId, DateTime startedUtc, CatalogueParentKind targetKind = CatalogueParentKind.TvShow)
    {
        lock (_markers)
        {
            _markers[podcastId] = new Marker(Guid.NewGuid(), targetKind, startedUtc, startedUtc);
        }
    }

    /// <summary>Another attempt takes the marker over, so the current holder's next heartbeat finds it lost.</summary>
    public void StealMarker(Guid podcastId, DateTime nowUtc)
    {
        lock (_markers)
        {
            var targetKind = _markers.TryGetValue(podcastId, out var existing)
                ? existing.TargetKind
                : CatalogueParentKind.TvShow;
            _markers[podcastId] = new Marker(Guid.NewGuid(), targetKind, nowUtc, nowUtc);
        }
    }

    public bool IsHeld(Guid podcastId)
    {
        lock (_markers)
        {
            return _markers.ContainsKey(podcastId);
        }
    }

    public Task<ParentKindTransferLockAcquisition> TryAcquire(
        Guid podcastId,
        CatalogueParentKind targetKind,
        DateTime nowUtc,
        TimeSpan staleAfter)
    {
        lock (_markers)
        {
            var outcome = ParentKindTransferLockOutcome.Acquired;
            DateTime? runningSince = null;
            CatalogueParentKind? previousTargetKind = null;
            if (_markers.TryGetValue(podcastId, out var existing))
            {
                if (nowUtc - existing.LastAliveUtc < staleAfter)
                {
                    return Task.FromResult(ParentKindTransferLockAcquisition.AlreadyRunning(existing.StartedUtc));
                }

                outcome = ParentKindTransferLockOutcome.ResumedStale;
                runningSince = existing.StartedUtc;
                previousTargetKind = existing.TargetKind;
            }

            var attemptId = Guid.NewGuid();
            _markers[podcastId] = new Marker(attemptId, targetKind, nowUtc, nowUtc);
            return Task.FromResult(new ParentKindTransferLockAcquisition(
                outcome,
                new ParentKindTransferLockHandle(podcastId, attemptId, attemptId.ToString(), targetKind, nowUtc),
                runningSince,
                previousTargetKind));
        }
    }

    public Task<ParentKindTransferLockHandle?> Renew(ParentKindTransferLockHandle handle, DateTime nowUtc)
    {
        lock (_markers)
        {
            RenewAttempts++;
            if (RenewError is { } error)
            {
                return Task.FromException<ParentKindTransferLockHandle?>(error);
            }

            if (!_markers.TryGetValue(handle.PodcastId, out var existing) || existing.AttemptId != handle.AttemptId)
            {
                return Task.FromResult<ParentKindTransferLockHandle?>(null);
            }

            _markers[handle.PodcastId] = existing with { LastAliveUtc = nowUtc };
            Renewals.Add(nowUtc);
            return Task.FromResult<ParentKindTransferLockHandle?>(handle);
        }
    }

    public Task Release(ParentKindTransferLockHandle handle)
    {
        lock (_markers)
        {
            if (_markers.TryGetValue(handle.PodcastId, out var existing) && existing.AttemptId == handle.AttemptId)
            {
                _markers.Remove(handle.PodcastId);
            }
        }

        return Task.CompletedTask;
    }
}
