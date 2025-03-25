using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices;

namespace Indexer;

[DurableTask(nameof(IndexIdProvider))]
public class IndexIdProvider(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    ILogger<IndexIdProvider> logger
) : TaskActivity<object, IndexIds>
{
    public override async Task<IndexIds> RunAsync(TaskActivityContext context, object input)
    {
        logger.LogInformation(
            $"{nameof(IndexIdProvider)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");

        var allIndexablePodcastIds= await indexablePodcastIdProvider.GetIndexablePodcastIds().ToArrayAsync();

        var halfCount= allIndexablePodcastIds.Length / 2;
        var list1 = allIndexablePodcastIds.Take(halfCount).ToArray();
        var list2 = allIndexablePodcastIds.Skip(halfCount).ToArray();

        logger.LogInformation("list1: {list1}", list1);
        logger.LogInformation("list2: {list2}", list2);

        logger.LogInformation($"{nameof(RunAsync)} Completed.");
        return new IndexIds(list1, list2);
    }
}