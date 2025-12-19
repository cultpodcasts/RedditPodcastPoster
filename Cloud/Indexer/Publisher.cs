using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Search;

namespace Indexer;

[DurableTask(nameof(Publisher))]
public class Publisher(
    IContentPublisher contentPublisher,
    ISearchIndexerService searchIndexerService,
    IActivityOptionsProvider activityOptionsProvider,
    ILogger<Publisher> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            "{nameofPublisher} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Publisher), context.InstanceId);
        ;
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunPublisher())
        {
            return indexerContext with { Success = true };
        }

        if (indexerContext.PublisherOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.PublisherOperationId));
        }

        try
        {
            await searchIndexerService.RunIndexer();
            await contentPublisher.PublishHomepage();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {nameofIContentPublisher}.{nameofIContentPublisherPublishHomepage}.",
                nameof(IContentPublisher), nameof(IContentPublisher.PublishHomepage));
            return indexerContext with { Success = false };
        }

        logger.LogInformation("{nameofRunAsync} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}