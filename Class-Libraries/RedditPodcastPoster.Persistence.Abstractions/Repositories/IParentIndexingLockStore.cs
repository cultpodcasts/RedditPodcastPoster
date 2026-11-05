using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Single-flight guard for parent indexing (podcast / TV show / news organisation). One marker per parent in LookUps
/// (id = <see cref="ParentIndexingLock.IdFor"/>); every write is etag-checked so two indexes cannot both hold it.
/// </summary>
public interface IParentIndexingLockStore
{
    /// <summary>
    /// Takes the marker for <paramref name="parentId"/>. A marker whose last heartbeat is younger than
    /// <paramref name="staleAfter"/> means another index is running, so this returns
    /// <see cref="ParentIndexingLockAcquisition.AlreadyRunning"/>. An older marker is taken over.
    /// </summary>
    Task<ParentIndexingLockAcquisition> TryAcquire(
        Guid parentId,
        DateTime nowUtc,
        TimeSpan staleAfter,
        string? source = null);

    /// <summary>
    /// Heartbeat: rewrites the marker's heartbeat with If-Match on this attempt's etag. Returns the new handle, or
    /// null when the marker was taken over or removed (the lease is lost and the holder must stop).
    /// </summary>
    Task<ParentIndexingLockHandle?> Renew(ParentIndexingLockHandle handle, DateTime nowUtc);

    /// <summary>Removes the marker if this attempt still holds it. Never throws for a lost or missing marker.</summary>
    Task Release(ParentIndexingLockHandle handle);

    /// <summary>Reads the marker for <paramref name="parentId"/>, or null when none exists.</summary>
    Task<ParentIndexingLock?> TryGet(Guid parentId);
}

public enum ParentIndexingLockOutcome
{
    Acquired,
    ResumedStale,
    AlreadyRunning
}

public sealed record ParentIndexingLockHandle(
    Guid ParentId,
    Guid AttemptId,
    string? ETag,
    DateTime StartedUtc,
    string? Source);

public sealed record ParentIndexingLockAcquisition(
    ParentIndexingLockOutcome Outcome,
    ParentIndexingLockHandle? Handle = null,
    DateTime? RunningSinceUtc = null)
{
    public static ParentIndexingLockAcquisition AlreadyRunning(DateTime? runningSinceUtc) =>
        new(ParentIndexingLockOutcome.AlreadyRunning, RunningSinceUtc: runningSinceUtc);

    public bool Held => Handle is not null;
}
