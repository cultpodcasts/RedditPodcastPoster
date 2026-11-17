using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Search.Services;

namespace Indexer.Activities;

[DurableTask(nameof(Publisher))]
public class Publisher(
    IHomepagePublisher contentPublisher,
    ISearchIndexerService searchIndexerService,
    ID1SearchChangeFeed d1SearchChangeFeed,
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
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        await DripIntoD1();
        memoryProbe.End();

        logger.LogInformation("{nameofRunAsync} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }

    /// <summary>
    /// SP-2.1 (#1321): the D1 search index's hourly sync, next to the Azure pull indexer run above, after the
    /// homepage so it never delays it. The change feed logs and swallows D1 and Cosmos failures; this guard keeps any
    /// other surprise out of the activity result, so D1 can never fail the hour or Azure indexing.
    /// </summary>
    private async Task DripIntoD1()
    {
        try
        {
            var result = await d1SearchChangeFeed.Drip(CancellationToken.None);
            if (result.Ran && !result.Succeeded)
            {
                logger.LogWarning(
                    "{class}: D1 search drip incomplete (upserts {Upserts}, deletes {Deletes}, not built {FailedToBuild}, " +
                    "write failures {WritesFailed}); the next hour repeats it.",
                    nameof(Publisher), result.Upserts, result.Deletes, result.FailedToBuild, result.WritesFailed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{class}: D1 search drip failed; Azure indexing and the homepage are unaffected.",
                nameof(Publisher));
        }
    }
}