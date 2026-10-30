using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Single-flight guard for podcast kind transfers. One marker per podcast in LookUps; every write is etag-checked
/// so two transfers cannot both hold it.
/// </summary>
public interface IParentKindTransferLockStore
{
    /// <summary>
    /// Takes the marker for <paramref name="podcastId"/>. A marker younger than <paramref name="staleAfter"/> means
    /// another transfer is running, so this returns <see cref="ParentKindTransferLockAcquisition.AlreadyRunning"/>.
    /// An older marker is taken over (resume).
    /// </summary>
    Task<ParentKindTransferLockAcquisition> TryAcquire(
        Guid podcastId,
        CatalogueParentKind targetKind,
        DateTime nowUtc,
        TimeSpan staleAfter);

    /// <summary>Removes the marker if this attempt still holds it. Never throws for a lost or missing marker.</summary>
    Task Release(ParentKindTransferLockHandle handle);
}

public enum ParentKindTransferLockOutcome
{
    Acquired,
    ResumedStale,
    AlreadyRunning
}

public sealed record ParentKindTransferLockHandle(Guid PodcastId, Guid AttemptId, string? ETag);

public sealed record ParentKindTransferLockAcquisition(
    ParentKindTransferLockOutcome Outcome,
    ParentKindTransferLockHandle? Handle = null,
    DateTime? RunningSinceUtc = null)
{
    public static ParentKindTransferLockAcquisition AlreadyRunning(DateTime? runningSinceUtc) =>
        new(ParentKindTransferLockOutcome.AlreadyRunning, RunningSinceUtc: runningSinceUtc);

    public bool Held => Handle is not null;
}
