using System.Net;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Search;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// The D1 search reconcile watermark lives in the <c>LookUps</c> container as a singleton partitioned by its own
/// id, like the other LookUps cursors. It is read and written by id only (one point read and one upsert per run).
/// </summary>
public sealed class D1SearchReconcileWatermarkStore(Container lookUpsContainer) : ID1SearchReconcileWatermarkStore
{
    private static readonly string Id = D1SearchReconcileWatermark._Id.ToString();

    public async Task<D1SearchReconcileWatermark?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await lookUpsContainer.ReadItemAsync<D1SearchReconcileWatermark>(
                Id, new PartitionKey(Id), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(D1SearchReconcileWatermark watermark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        if (watermark.Id != D1SearchReconcileWatermark._Id)
        {
            throw new ArgumentException(
                "The D1 search reconcile watermark is a singleton with a fixed id.", nameof(watermark));
        }

        await lookUpsContainer.UpsertItemAsync(watermark, new PartitionKey(Id), cancellationToken: cancellationToken);
    }
}
