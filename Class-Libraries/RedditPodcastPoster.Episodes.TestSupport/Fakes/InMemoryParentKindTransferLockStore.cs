using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>In-memory single-flight markers with the same fresh / stale rules as the LookUps store.</summary>
public sealed class InMemoryParentKindTransferLockStore : IParentKindTransferLockStore
{
    private readonly Dictionary<Guid, (Guid AttemptId, DateTime StartedUtc)> _markers = new();

    public void SeedMarker(Guid podcastId, DateTime startedUtc)
    {
        lock (_markers)
        {
            _markers[podcastId] = (Guid.NewGuid(), startedUtc);
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
            if (_markers.TryGetValue(podcastId, out var existing))
            {
                if (nowUtc - existing.StartedUtc < staleAfter)
                {
                    return Task.FromResult(ParentKindTransferLockAcquisition.AlreadyRunning(existing.StartedUtc));
                }

                outcome = ParentKindTransferLockOutcome.ResumedStale;
                runningSince = existing.StartedUtc;
            }

            var attemptId = Guid.NewGuid();
            _markers[podcastId] = (attemptId, nowUtc);
            return Task.FromResult(new ParentKindTransferLockAcquisition(
                outcome,
                new ParentKindTransferLockHandle(podcastId, attemptId, attemptId.ToString()),
                runningSince));
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
