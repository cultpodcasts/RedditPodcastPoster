using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Single-flight guard for podcast kind transfers. One marker per podcast in LookUps; every write is etag-checked
/// so two transfers cannot both hold it.
/// </summary>
public interface IParentKindTransferLockStore
{
    /// <summary>
    /// Takes the marker for <paramref name="podcastId"/>. A marker whose last heartbeat is younger than <paramref name="staleAfter"/> means
    /// another transfer is running, so this returns <see cref="ParentKindTransferLockAcquisition.AlreadyRunning"/>.
    /// An older marker is taken over (resume); <see cref="ParentKindTransferLockAcquisition.PreviousTargetKind"/>
    /// then says which kind that earlier transfer was moving to.
    /// </summary>
    Task<ParentKindTransferLockAcquisition> TryAcquire(
        Guid podcastId,
        CatalogueParentKind targetKind,
        DateTime nowUtc,
        TimeSpan staleAfter);

    /// <summary>
    /// Heartbeat: rewrites the marker's heartbeat with If-Match on this attempt's etag. Returns the new handle, or
    /// null when the marker was taken over or removed (the lease is lost and the holder must stop). Other errors
    /// throw; the caller may retry on its next heartbeat.
    /// </summary>
    Task<ParentKindTransferLockHandle?> Renew(ParentKindTransferLockHandle handle, DateTime nowUtc);

    /// <summary>Removes the marker if this attempt still holds it. Never throws for a lost or missing marker.</summary>
    Task Release(ParentKindTransferLockHandle handle);
}

public enum ParentKindTransferLockOutcome
{
    Acquired,
    ResumedStale,
    AlreadyRunning
}

public sealed record ParentKindTransferLockHandle(
    Guid PodcastId,
    Guid AttemptId,
    string? ETag,
    CatalogueParentKind TargetKind,
    DateTime StartedUtc);

public sealed record ParentKindTransferLockAcquisition(
    ParentKindTransferLockOutcome Outcome,
    ParentKindTransferLockHandle? Handle = null,
    DateTime? RunningSinceUtc = null,
    CatalogueParentKind? PreviousTargetKind = null)
{
    public static ParentKindTransferLockAcquisition AlreadyRunning(DateTime? runningSinceUtc) =>
        new(ParentKindTransferLockOutcome.AlreadyRunning, RunningSinceUtc: runningSinceUtc);

    public bool Held => Handle is not null;
}
