using System.Collections.Concurrent;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// LookUps claim per file key (id = <c>filekey:{lowercased key}</c>, partition = id).
/// <c>CreateItemAsync</c> fails with 409 when another owner holds it. The same owner may retry.
/// Only the call that creates the document tracks it. A second process that sees the same owner's
/// fresh uncommitted claim does not track it and does not replace <see cref="FileKeyClaim.ScopeId"/>,
/// so disposing that process cannot delete the creator's key. The lease is adopted with If-Match only
/// once the claim is past <see cref="FileKeyClaim.UncommittedLifetimeSeconds"/>. A save acknowledges the
/// claim so it is kept. <see cref="ReleaseUnacknowledged"/> deletes only when the stored scope id still
/// matches. An unacknowledged claim expires after a day if the process dies first.
/// </summary>
public sealed class FileKeyClaimStore(
    Container lookUpsContainer,
    ILogger<FileKeyClaimStore> logger) : IFileKeyClaimStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Guid>> _scopes = new();

    public async Task<FileKeyClaimResult> TryClaim(
        string fileKey,
        Guid ownerId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var id = FileKeyClaim.DocumentIdFor(fileKey);
        var marker = UncommittedMarker(id, ownerId, scopeId);
        var partition = new PartitionKey(id);

        if (await TryCreate(marker, partition, cancellationToken))
        {
            Track(scopeId, fileKey, ownerId);
            return FileKeyClaimResult.Created;
        }

        var read = await ReadClaim(id, partition, cancellationToken);
        if (read == null)
        {
            if (await TryCreate(marker, partition, cancellationToken))
            {
                Track(scopeId, fileKey, ownerId);
                return FileKeyClaimResult.Created;
            }

            return FileKeyClaimResult.HeldByOther;
        }

        return await ResolveHeldClaim(read, marker, id, partition, fileKey, ownerId, scopeId, cancellationToken);
    }

    public async Task Acknowledge(string? fileKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey) || !Untrack(fileKey, out var ownerId))
        {
            return;
        }

        var id = FileKeyClaim.DocumentIdFor(fileKey);
        var partition = new PartitionKey(id);
        try
        {
            var read = await lookUpsContainer.ReadItemAsync<FileKeyClaim>(id, partition, cancellationToken: cancellationToken);
            var marker = read.Resource;
            if (marker.OwnerId != ownerId)
            {
                return;
            }

            marker.Committed = true;
            marker.TimeToLiveSeconds = -1;
            await lookUpsContainer.ReplaceItemAsync(marker, id, partition, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Released between the save and the keep.
        }
    }

    public async Task ReleaseUnacknowledged(Guid scopeId, CancellationToken cancellationToken = default)
    {
        if (!_scopes.TryRemove(scopeId, out var pending))
        {
            return;
        }

        foreach (var (fileKey, ownerId) in pending)
        {
            await DeleteIfOwner(fileKey, ownerId, releasingScopeId: scopeId, cancellationToken);
        }
    }

    public Task Delete(string fileKey, Guid ownerId, CancellationToken cancellationToken = default)
    {
        Untrack(fileKey, out _);
        return DeleteIfOwner(fileKey, ownerId, releasingScopeId: null, cancellationToken);
    }

    private async Task<FileKeyClaimResult> ResolveHeldClaim(
        ItemResponse<FileKeyClaim> read,
        FileKeyClaim marker,
        string id,
        PartitionKey partition,
        string fileKey,
        Guid ownerId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var existing = read.Resource;
        if (existing.OwnerId != ownerId)
        {
            return FileKeyClaimResult.HeldByOther;
        }

        // Another function instance has an empty scope map. That is not a reason to take the lease:
        // its dispose would delete the document while the creator is still in flight. A dead creator
        // expires after the one-day ttl. Adopt only once that lifetime has passed, the way a transfer
        // lock waits out its heartbeat window before replacing the marker.
        if (existing.Committed || !FileKeyClaim.IsPastUncommittedLifetime(existing.ClaimedUtc, DateTime.UtcNow))
        {
            return FileKeyClaimResult.AlreadyOurs;
        }

        try
        {
            await lookUpsContainer.ReplaceItemAsync(
                marker,
                id,
                partition,
                new ItemRequestOptions { IfMatchEtag = read.ETag },
                cancellationToken);
            Track(scopeId, fileKey, ownerId);
            return FileKeyClaimResult.AlreadyOurs;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            var again = await ReadClaim(id, partition, cancellationToken);
            if (again == null || again.Resource.OwnerId != ownerId)
            {
                return FileKeyClaimResult.HeldByOther;
            }

            // Lost the etag race. Do not track: the scope that won the lease releases or acknowledges.
            return FileKeyClaimResult.AlreadyOurs;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            if (await TryCreate(marker, partition, cancellationToken))
            {
                Track(scopeId, fileKey, ownerId);
                return FileKeyClaimResult.Created;
            }

            return FileKeyClaimResult.HeldByOther;
        }
    }

    private async Task DeleteIfOwner(
        string fileKey,
        Guid ownerId,
        Guid? releasingScopeId,
        CancellationToken cancellationToken)
    {
        var id = FileKeyClaim.DocumentIdFor(fileKey);
        var partition = new PartitionKey(id);
        try
        {
            var read = await lookUpsContainer.ReadItemAsync<FileKeyClaim>(id, partition, cancellationToken: cancellationToken);
            var marker = read.Resource;
            if (marker.OwnerId != ownerId)
            {
                return;
            }

            if (releasingScopeId is Guid scopeId)
            {
                if (marker.Committed)
                {
                    return;
                }

                if (marker.ScopeId != scopeId)
                {
                    logger.LogInformation(
                        "{method}: File-key claim '{fileKey}' is leased to another scope; leaving it.",
                        nameof(ReleaseUnacknowledged),
                        fileKey);
                    return;
                }
            }

            await lookUpsContainer.DeleteItemAsync<FileKeyClaim>(
                id,
                partition,
                new ItemRequestOptions { IfMatchEtag = read.ETag },
                cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            logger.LogInformation(
                "{method}: File-key claim '{fileKey}' was already gone or replaced ({status}).",
                nameof(Delete),
                fileKey,
                ex.StatusCode);
        }
        catch (CosmosException)
        {
            // 429 and other Cosmos failures must reach the caller. A committed claim has ttl -1, so swallowing
            // a throttled compensation delete would leave the key reserved with no parent. RetryThrottled retries 429.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Failed to release file-key claim '{fileKey}'.", nameof(Delete), fileKey);
        }
    }

    private async Task<bool> TryCreate(FileKeyClaim marker, PartitionKey partition, CancellationToken cancellationToken)
    {
        try
        {
            await lookUpsContainer.CreateItemAsync(marker, partition, cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    private async Task<ItemResponse<FileKeyClaim>?> ReadClaim(
        string id,
        PartitionKey partition,
        CancellationToken cancellationToken)
    {
        try
        {
            return await lookUpsContainer.ReadItemAsync<FileKeyClaim>(id, partition, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static FileKeyClaim UncommittedMarker(string id, Guid ownerId, Guid scopeId) =>
        new()
        {
            Id = id,
            OwnerId = ownerId,
            ScopeId = scopeId,
            ClaimedUtc = DateTime.UtcNow,
            Committed = false,
            TimeToLiveSeconds = FileKeyClaim.UncommittedLifetimeSeconds
        };

    private void Track(Guid scopeId, string fileKey, Guid ownerId)
    {
        var pending = _scopes.GetOrAdd(scopeId, _ => new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
        pending[fileKey] = ownerId;
    }

    private bool Untrack(string fileKey, out Guid ownerId)
    {
        ownerId = Guid.Empty;
        var found = false;
        foreach (var pending in _scopes.Values)
        {
            if (pending.TryRemove(fileKey, out var owner))
            {
                ownerId = owner;
                found = true;
            }
        }

        return found;
    }
}
