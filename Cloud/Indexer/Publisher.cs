using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;

namespace Indexer;

[DurableTask(nameof(Publisher))]
public class Publisher(
    IContentPublisher contentPublisher,
    IActivityMarshaller activityMarshaller,
    ILogger<Publisher> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Publisher)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());

        if (DryRun.IsPublisherDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.PublisherOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.PublisherOperationId));
        }

        try
        {
            await contentPublisher.PublishHomepage();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to execute {nameof(IContentPublisher)}.{nameof(IContentPublisher.PublishHomepage)}.");
            return indexerContext with {Success = false};
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}