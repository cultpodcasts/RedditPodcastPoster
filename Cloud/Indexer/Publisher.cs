using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Search;

namespace Indexer;

[DurableTask(nameof(Publisher))]
public class Publisher(
    IHomepagePublisher contentPublisher,
    ISearchIndexerService searchIndexerService,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Publisher> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Publisher));

        logger.LogInformation(
            "{nameofPublisher} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Publisher), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunPublisher(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Publisher), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Publisher), reason);
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
                "Failure to execute {PublisherContract}.{PublishHomepageMethod}.",
                nameof(IHomepagePublisher), nameof(IHomepagePublisher.PublishHomepage));

            memoryProbe.End(false, ex.GetType().Name);

            return indexerContext with { Success = false };
        }

        memoryProbe.End();

        logger.LogInformation("{nameofRunAsync} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}