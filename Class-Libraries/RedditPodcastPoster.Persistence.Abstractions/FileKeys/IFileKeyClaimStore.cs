namespace RedditPodcastPoster.Persistence.Abstractions.FileKeys;

public enum FileKeyClaimResult
{
    /// <summary>This call created the claim.</summary>
    Created = 0,

    /// <summary>This owner already holds the claim (a retry of the same create).</summary>
    AlreadyOurs = 1,

    /// <summary>Another owner holds the claim. The caller tries the next suffix.</summary>
    HeldByOther = 2
}

/// <summary>
/// Cross-process file-key claims in the <c>FileKeyClaims</c> container. One document per key, created with
/// <c>CreateItemAsync</c>. A 409 is <see cref="FileKeyClaimResult.HeldByOther"/> unless the same owner already holds it.
/// </summary>
public interface IFileKeyClaimStore
{
    Task<FileKeyClaimResult> TryClaim(string fileKey, Guid ownerId, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The item with this key was saved. The claim is kept and is no longer released with the scope.
    /// A key this process did not claim is ignored, so an ordinary save does not read Cosmos.
    /// </summary>
    Task Acknowledge(string? fileKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes claims this scope created and has not acknowledged, when the stored scope id still matches.
    /// A claim whose lease moved to another scope is left alone.
    /// </summary>
    Task ReleaseUnacknowledged(Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the claim even after it was kept. Used when the saved item is removed.</summary>
    Task Delete(string fileKey, Guid ownerId, CancellationToken cancellationToken = default);
}
