using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// LookUps marker per parent (id = <see cref="ParentIndexingLock.IdFor"/>, partition = id). Create fails with 409 when
/// another index holds it. The holder renews a heartbeat with If-Match on its etag. A marker whose last heartbeat is
/// older than the stale window is replaced with If-Match on the etag that was read, so only one taker wins. Release
/// deletes with If-Match on this attempt's etag, so a taken-over marker is left to its new holder.
/// </summary>
public sealed class ParentIndexingLockStore(
    Container lookUpsContainer,
    ILogger<ParentIndexingLockStore> logger) : IParentIndexingLockStore
{
    public async Task<ParentIndexingLockAcquisition> TryAcquire(
        Guid parentId,
        DateTime nowUtc,
        TimeSpan staleAfter,
        string? source = null)
    {
        var lockId = ParentIndexingLock.IdFor(parentId);
        var marker = new ParentIndexingLock
        {
            Id = lockId,
            ParentId = parentId,
            StartedUtc = nowUtc,
            AttemptId = Guid.NewGuid(),
            Source = source
        };
        var partitionKey = new PartitionKey(lockId.ToString());

        // Two passes: a marker released between our create and our read is retried once as a fresh create.
        for (var pass = 0; pass < 2; pass++)
        {
            try
            {
                var created = await lookUpsContainer.CreateItemAsync(marker, partitionKey);
                return new ParentIndexingLockAcquisition(
                    ParentIndexingLockOutcome.Acquired,
                    HandleFor(marker, created.ETag));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Held by someone; inspect it below.
            }

            ItemResponse<ParentIndexingLock> existing;
            try
            {
                existing = await lookUpsContainer.ReadItemAsync<ParentIndexingLock>(
                    lockId.ToString(), partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            var current = existing.Resource;
            if (nowUtc - current.LastAliveUtc < staleAfter)
            {
                return ParentIndexingLockAcquisition.AlreadyRunning(current.StartedUtc);
            }

            try
            {
                var replaced = await lookUpsContainer.ReplaceItemAsync(
                    marker,
                    lockId.ToString(),
                    partitionKey,
                    new ItemRequestOptions { IfMatchEtag = existing.ETag });
                logger.LogWarning(
                    "{method}: Took over a stale indexing marker for parent '{parentId}' (source {previousSource}, started {startedUtc:O}, last heartbeat {lastAliveUtc:O}); now indexing as {source}.",
                    nameof(TryAcquire),
                    parentId,
                    current.Source,
                    current.StartedUtc,
                    current.LastAliveUtc,
                    source);
                return new ParentIndexingLockAcquisition(
                    ParentIndexingLockOutcome.ResumedStale,
                    HandleFor(marker, replaced.ETag),
                    current.StartedUtc);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Another request took the stale marker first.
                return ParentIndexingLockAcquisition.AlreadyRunning(nowUtc);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Released between read and replace; try a fresh create.
            }
        }

        return ParentIndexingLockAcquisition.AlreadyRunning(nowUtc);
    }

    public async Task<ParentIndexingLockHandle?> Renew(ParentIndexingLockHandle handle, DateTime nowUtc)
    {
        var lockId = ParentIndexingLock.IdFor(handle.ParentId);
        var marker = new ParentIndexingLock
        {
            Id = lockId,
            ParentId = handle.ParentId,
            StartedUtc = handle.StartedUtc,
            AttemptId = handle.AttemptId,
            HeartbeatUtc = nowUtc,
            Source = handle.Source
        };
        try
        {
            var renewed = await lookUpsContainer.ReplaceItemAsync(
                marker,
                lockId.ToString(),
                new PartitionKey(lockId.ToString()),
                new ItemRequestOptions { IfMatchEtag = handle.ETag });
            return handle with { ETag = renewed.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            logger.LogWarning(
                "{method}: Lost the indexing marker for parent '{parentId}' ({status}).",
                nameof(Renew),
                handle.ParentId,
                ex.StatusCode);
            return null;
        }
    }

    public async Task Release(ParentIndexingLockHandle handle)
    {
        var lockId = ParentIndexingLock.IdFor(handle.ParentId);
        try
        {
            await lookUpsContainer.DeleteItemAsync<ParentIndexingLock>(
                lockId.ToString(),
                new PartitionKey(lockId.ToString()),
                new ItemRequestOptions { IfMatchEtag = handle.ETag });
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            logger.LogInformation(
                "{method}: Indexing marker for parent '{parentId}' was already gone or taken over ({status}).",
                nameof(Release),
                handle.ParentId,
                ex.StatusCode);
        }
        catch (Exception ex)
        {
            // The index outcome stands; a leftover marker goes stale and the next index takes it over.
            logger.LogWarning(
                ex,
                "{method}: Failed to release indexing marker for parent '{parentId}'.",
                nameof(Release),
                handle.ParentId);
        }
    }

    public async Task<ParentIndexingLock?> TryGet(Guid parentId)
    {
        var lockId = ParentIndexingLock.IdFor(parentId);
        try
        {
            var response = await lookUpsContainer.ReadItemAsync<ParentIndexingLock>(
                lockId.ToString(), new PartitionKey(lockId.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static ParentIndexingLockHandle HandleFor(ParentIndexingLock marker, string? etag) =>
        new(marker.ParentId, marker.AttemptId, etag, marker.StartedUtc, marker.Source);
}
