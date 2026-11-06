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
/// A save acknowledges the claim so it is kept. The scope releases any claim it created and did not
/// acknowledge. An unacknowledged claim expires after a day if the process dies first.
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
        var marker = new FileKeyClaim
        {
            Id = id,
            OwnerId = ownerId,
            Committed = false,
            TimeToLiveSeconds = FileKeyClaim.UncommittedLifetimeSeconds
        };
        var partition = new PartitionKey(id);

        try
        {
            await lookUpsContainer.CreateItemAsync(marker, partition, cancellationToken: cancellationToken);
            Track(scopeId, fileKey, ownerId);
            return FileKeyClaimResult.Created;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Held by someone; the same owner is a retry.
        }

        FileKeyClaim? existing;
        try
        {
            var read = await lookUpsContainer.ReadItemAsync<FileKeyClaim>(id, partition, cancellationToken: cancellationToken);
            existing = read.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            existing = null;
        }

        if (existing == null)
        {
            try
            {
                await lookUpsContainer.CreateItemAsync(marker, partition, cancellationToken: cancellationToken);
                Track(scopeId, fileKey, ownerId);
                return FileKeyClaimResult.Created;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                return FileKeyClaimResult.HeldByOther;
            }
        }

        if (existing.OwnerId != ownerId)
        {
            return FileKeyClaimResult.HeldByOther;
        }

        if (!existing.Committed)
        {
            Track(scopeId, fileKey, ownerId);
        }

        return FileKeyClaimResult.AlreadyOurs;
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
            await DeleteIfUncommitted(fileKey, ownerId, cancellationToken);
        }
    }

    public Task Delete(string fileKey, Guid ownerId, CancellationToken cancellationToken = default)
    {
        Untrack(fileKey, out _);
        return DeleteIfOwner(fileKey, ownerId, onlyIfUncommitted: false, cancellationToken);
    }

    private async Task DeleteIfUncommitted(string fileKey, Guid ownerId, CancellationToken cancellationToken)
    {
        await DeleteIfOwner(fileKey, ownerId, onlyIfUncommitted: true, cancellationToken);
    }

    private async Task DeleteIfOwner(
        string fileKey,
        Guid ownerId,
        bool onlyIfUncommitted,
        CancellationToken cancellationToken)
    {
        var id = FileKeyClaim.DocumentIdFor(fileKey);
        var partition = new PartitionKey(id);
        try
        {
            var read = await lookUpsContainer.ReadItemAsync<FileKeyClaim>(id, partition, cancellationToken: cancellationToken);
            var marker = read.Resource;
            if (marker.OwnerId != ownerId || (onlyIfUncommitted && marker.Committed))
            {
                return;
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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Failed to release file-key claim '{fileKey}'.", nameof(Delete), fileKey);
        }
    }

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
