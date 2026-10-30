using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// LookUps marker per podcast (id = podcast id, partition = id). Create fails with 409 when another transfer holds
/// it. The holder renews a heartbeat with If-Match on its etag. A marker whose last heartbeat is older than the stale
/// window is replaced with If-Match on the etag that was read, so only one taker wins. Release deletes with If-Match
/// on this attempt's etag, so a taken-over marker is left to its new holder.
/// </summary>
public sealed class ParentKindTransferLockStore(
    Container lookUpsContainer,
    ILogger<ParentKindTransferLockStore> logger) : IParentKindTransferLockStore
{
    public async Task<ParentKindTransferLockAcquisition> TryAcquire(
        Guid podcastId,
        CatalogueParentKind targetKind,
        DateTime nowUtc,
        TimeSpan staleAfter)
    {
        var marker = new ParentKindTransferLock
        {
            Id = podcastId,
            TargetKind = targetKind,
            StartedUtc = nowUtc,
            AttemptId = Guid.NewGuid()
        };
        var partitionKey = new PartitionKey(podcastId.ToString());

        // Two passes: a marker released between our create and our read is retried once as a fresh create.
        for (var pass = 0; pass < 2; pass++)
        {
            try
            {
                var created = await lookUpsContainer.CreateItemAsync(marker, partitionKey);
                return new ParentKindTransferLockAcquisition(
                    ParentKindTransferLockOutcome.Acquired,
                    HandleFor(marker, created.ETag));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Held by someone; inspect it below.
            }

            ItemResponse<ParentKindTransferLock> existing;
            try
            {
                existing = await lookUpsContainer.ReadItemAsync<ParentKindTransferLock>(
                    podcastId.ToString(), partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            var current = existing.Resource;
            if (nowUtc - current.LastAliveUtc < staleAfter)
            {
                return ParentKindTransferLockAcquisition.AlreadyRunning(current.StartedUtc);
            }

            try
            {
                var replaced = await lookUpsContainer.ReplaceItemAsync(
                    marker,
                    podcastId.ToString(),
                    partitionKey,
                    new ItemRequestOptions { IfMatchEtag = existing.ETag });
                logger.LogWarning(
                    "{method}: Took over a stale kind transfer marker for podcast '{podcastId}' (to {previousTargetKind}, started {startedUtc:O}, last heartbeat {lastAliveUtc:O}); now transferring to {targetKind}.",
                    nameof(TryAcquire),
                    podcastId,
                    current.TargetKind,
                    current.StartedUtc,
                    current.LastAliveUtc,
                    targetKind);
                return new ParentKindTransferLockAcquisition(
                    ParentKindTransferLockOutcome.ResumedStale,
                    HandleFor(marker, replaced.ETag),
                    current.StartedUtc,
                    current.TargetKind);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Another request took the stale marker first.
                return ParentKindTransferLockAcquisition.AlreadyRunning(nowUtc);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Released between read and replace; try a fresh create.
            }
        }

        return ParentKindTransferLockAcquisition.AlreadyRunning(nowUtc);
    }

    public async Task<ParentKindTransferLockHandle?> Renew(ParentKindTransferLockHandle handle, DateTime nowUtc)
    {
        var marker = new ParentKindTransferLock
        {
            Id = handle.PodcastId,
            TargetKind = handle.TargetKind,
            StartedUtc = handle.StartedUtc,
            AttemptId = handle.AttemptId,
            HeartbeatUtc = nowUtc
        };
        try
        {
            var renewed = await lookUpsContainer.ReplaceItemAsync(
                marker,
                handle.PodcastId.ToString(),
                new PartitionKey(handle.PodcastId.ToString()),
                new ItemRequestOptions { IfMatchEtag = handle.ETag });
            return handle with { ETag = renewed.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            logger.LogWarning(
                "{method}: Lost the kind transfer marker for podcast '{podcastId}' ({status}).",
                nameof(Renew),
                handle.PodcastId,
                ex.StatusCode);
            return null;
        }
    }

    private static ParentKindTransferLockHandle HandleFor(ParentKindTransferLock marker, string? etag) =>
        new(marker.Id, marker.AttemptId, etag, marker.TargetKind, marker.StartedUtc);

    public async Task Release(ParentKindTransferLockHandle handle)
    {
        try
        {
            await lookUpsContainer.DeleteItemAsync<ParentKindTransferLock>(
                handle.PodcastId.ToString(),
                new PartitionKey(handle.PodcastId.ToString()),
                new ItemRequestOptions { IfMatchEtag = handle.ETag });
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            logger.LogInformation(
                "{method}: Kind transfer marker for podcast '{podcastId}' was already gone or taken over ({status}).",
                nameof(Release),
                handle.PodcastId,
                ex.StatusCode);
        }
        catch (Exception ex)
        {
            // The transfer outcome stands; a leftover marker goes stale and the next transfer takes it over.
            logger.LogWarning(
                ex,
                "{method}: Failed to release kind transfer marker for podcast '{podcastId}'.",
                nameof(Release),
                handle.PodcastId);
        }
    }
}
