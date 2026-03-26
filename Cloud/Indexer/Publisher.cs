using System.Diagnostics;
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
    ILogger<Publisher> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var runStopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "{nameofPublisher} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Publisher), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogInformation("PublisherCostProbe.Start instance-id='{InstanceId}'.", context.InstanceId);
        }

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

        var searchIndexerMs = 0L;
        var homepagePublishMs = 0L;

        try
        {
            var searchIndexerStopwatch = Stopwatch.StartNew();
            await searchIndexerService.RunIndexer();
            searchIndexerStopwatch.Stop();
            searchIndexerMs = searchIndexerStopwatch.ElapsedMilliseconds;

            var homepageStopwatch = Stopwatch.StartNew();
            await contentPublisher.PublishHomepage();
            homepageStopwatch.Stop();
            homepagePublishMs = homepageStopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {PublisherContract}.{PublishHomepageMethod}.",
                nameof(IHomepagePublisher), nameof(IHomepagePublisher.PublishHomepage));

            runStopwatch.Stop();
            if (_indexerOptions.EnableCostInstrumentation)
            {
                logger.LogInformation(
                    "PublisherCostProbe.Complete instance-id='{InstanceId}' success='false' search-indexer-ms='{SearchIndexerMs}' homepage-publish-ms='{HomepagePublishMs}' total-ms='{TotalMs}' error-type='{ErrorType}'.",
                    context.InstanceId,
                    searchIndexerMs,
                    homepagePublishMs,
                    runStopwatch.ElapsedMilliseconds,
                    ex.GetType().Name);
            }

            return indexerContext with { Success = false };
        }

        runStopwatch.Stop();
        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogInformation(
                "PublisherCostProbe.Complete instance-id='{InstanceId}' success='true' search-indexer-ms='{SearchIndexerMs}' homepage-publish-ms='{HomepagePublishMs}' total-ms='{TotalMs}'.",
                context.InstanceId,
                searchIndexerMs,
                homepagePublishMs,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("{nameofRunAsync} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}