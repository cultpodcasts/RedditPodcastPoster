using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices;

namespace Indexer;

[DurableTask(nameof(IndexIdProvider))]
public class IndexIdProvider(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    ILogger<IndexIdProvider> logger
) : TaskActivity<IndexIdProviderRequest, IndexIdProviderResponse>
{
    public override async Task<IndexIdProviderResponse> RunAsync(TaskActivityContext context, IndexIdProviderRequest req)
    {
        logger.LogInformation(
            "{IndexIdProviderName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(IndexIdProvider), context.InstanceId);

        if (req.IndexPasses < 1)
        {
            throw new ArgumentException("IndexPasses must be greater than 0.");
        }
        var allIndexablePodcastIds = await indexablePodcastIdProvider.GetIndexablePodcastIds().ToArrayAsync();

        var batchSizes = allIndexablePodcastIds.Length / req.IndexPasses;
        var batches = new List<Guid[]>();
        for (var i = 0; i < req.IndexPasses; i++)
        {
            var batch = allIndexablePodcastIds.Skip(i * batchSizes);
            if (i < req.IndexPasses - 1)
            {
                batch = batch.Take(batchSizes);
            }

            var batchArray = batch.ToArray();
            logger.LogInformation("Batch {i}: {batch}", i + 1, batchArray);
            batches.Add(batchArray);
        }

        var batchSum = batches.Sum(batch => batch.Length);
        if (batchSum != allIndexablePodcastIds.Length)
        {
            throw new InvalidOperationException(
                $"Batch sum {batchSum} does not equal all indexable podcast ids {allIndexablePodcastIds.Length}.");
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed.");
        return new IndexIdProviderResponse(batches.ToArray());
    }
}