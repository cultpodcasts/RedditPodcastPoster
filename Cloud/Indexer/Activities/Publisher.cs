using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
using RedditPodcastPoster.Search.Services;

namespace Indexer.Activities;

[DurableTask(nameof(Publisher))]
public class Publisher(
    IHomepagePublisher contentPublisher,
    ISearchIndexerService searchIndexerService,
    ID1SearchChangeFeed d1SearchChangeFeed,
    IFilmSearchPusher filmSearchPusher,
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
            // D1 must not depend on Azure: the drip still runs when the Azure indexer or the homepage fails,
            // and the activity still fails as before. Films are pushed too: their push does not depend on the
            // episode indexer or the homepage.
            await PushFilms();
            await DripIntoD1();
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        await PushFilms();
        await DripIntoD1();
        memoryProbe.End();

        logger.LogInformation("{nameofRunAsync} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }

    /// <summary>
    /// #1408: films have no pull indexer (the Free tier's three data sources are used by episodes, TV show episodes
    /// and news reports), so the scheduled refresh pushes them: an incremental push by Cosmos <c>_ts</c> from the
    /// saved watermark. After an index teardown the pusher finds no film documents and makes it a full push, so films
    /// come back within the hour. The pusher keeps the watermark when anything fails, so the next run retries. A
    /// failure is logged and never fails the activity, the episode indexer run or the homepage publish.
    /// </summary>
    private async Task PushFilms()
    {
        try
        {
            var result = await filmSearchPusher.Push(new FilmSearchPushRequest(), CancellationToken.None);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "{class}: film search push incomplete ({Mode}; pushed {ToPush}, deleted {ToDelete}, failed {Failed}); " +
                    "the watermark was kept, so the next refresh retries.",
                    nameof(Publisher), result.FullPush ? "full" : "incremental", result.ToPush, result.ToDelete,
                    result.Failed);
            }
            else
            {
                logger.LogInformation(
                    "{class}: film search push done ({Mode}; read {FilmsRead}, pushed {ToPush}, deleted {ToDelete}; " +
                    "watermark {PreviousWatermark} -> {NewWatermark}).",
                    nameof(Publisher), result.FullPush ? "full" : "incremental", result.FilmsRead, result.ToPush,
                    result.ToDelete, result.PreviousWatermark, result.NewWatermark);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{class}: film search push failed; the episode indexer and the homepage are unaffected, and the next " +
                "refresh retries.", nameof(Publisher));
        }
    }

    /// <summary>
    /// SP-2.1 (#1321): the D1 search index's hourly sync, next to the Azure pull indexer run above, after the
    /// homepage so it never delays it. It also runs when the Azure block throws, so D1 never depends on Azure. The change feed logs and swallows D1 and Cosmos failures; this guard keeps any
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