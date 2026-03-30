using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices;

namespace Indexer;

[DurableTask(nameof(IndexIdProvider))]
public class IndexIdProvider(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<IndexIdProvider> logger
) : TaskActivity<IndexIdProviderRequest, IndexIdProviderResponse>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexIdProviderResponse> RunAsync(TaskActivityContext context, IndexIdProviderRequest req)
    {
        var runStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "{IndexIdProviderName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(IndexIdProvider), context.InstanceId);

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogWarning(
                "IndexIdProviderCostProbe.Start instance-id='{InstanceId}' index-passes='{IndexPasses}'.",
                context.InstanceId,
                req.IndexPasses);
        }

        if (req.IndexPasses < 1)
        {
            throw new ArgumentException("IndexPasses must be greater than 0.");
        }

        var queryStopwatch = Stopwatch.StartNew();
        var allIndexablePodcastIds = await indexablePodcastIdProvider.GetIndexablePodcastIds().ToArrayAsync();
        queryStopwatch.Stop();

        var currentHour = DateTime.UtcNow.Hour;
        var isEvenHour = currentHour % 2 == 0;
        var halfSize = allIndexablePodcastIds.Length / 2;
        var indexablePodcastIds = isEvenHour
            ? allIndexablePodcastIds.Take(halfSize).ToArray()
            : allIndexablePodcastIds.Skip(halfSize).ToArray();

        var batchSizes = indexablePodcastIds.Length / req.IndexPasses;
        var batches = new List<Guid[]>();
        for (var i = 0; i < req.IndexPasses; i++)
        {
            var batch = indexablePodcastIds.Skip(i * batchSizes);
            if (i < req.IndexPasses - 1)
            {
                batch = batch.Take(batchSizes);
            }

            var batchArray = batch.ToArray();
            logger.LogInformation("Batch {i}: {batch}", i + 1, batchArray);
            batches.Add(batchArray);
        }

        var batchSum = batches.Sum(batch => batch.Length);
        if (batchSum != indexablePodcastIds.Length)
        {
            throw new InvalidOperationException(
                $"Batch sum {batchSum} does not equal indexable podcast ids {indexablePodcastIds.Length}.");
        }

        runStopwatch.Stop();

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogWarning(
                "IndexIdProviderCostProbe.Complete instance-id='{InstanceId}' success='true' index-passes='{IndexPasses}' total-podcast-ids='{TotalPodcastIds}' half='{Half}' half-ids='{HalfIds}' batch-size-base='{BatchSizeBase}' query-ms='{QueryMs}' total-ms='{TotalMs}'.",
                context.InstanceId,
                req.IndexPasses,
                allIndexablePodcastIds.Length,
                isEvenHour ? "even" : "odd",
                indexablePodcastIds.Length,
                batchSizes,
                queryStopwatch.ElapsedMilliseconds,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed.");
        return new IndexIdProviderResponse(batches.ToArray());
    }
}