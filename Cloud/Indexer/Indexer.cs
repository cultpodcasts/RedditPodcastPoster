using System.Diagnostics;
using Azure;
using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Indexer;

[DurableTask(nameof(Indexer))]
public class Indexer(
    IPodcastsUpdater podcastsUpdater,
    IActivityMarshaller activityMarshaller,
    IIndexingStrategy indexingStrategy,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Indexer> logger)
    : TaskActivity<IndexerContextWrapper, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<IndexerContext> RunAsync(
        TaskActivityContext context, IndexerContextWrapper indexerContextWrapper)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Indexer));
        ActivityStatus initiatedStatus = ActivityStatus.Unknown;
        ActivityStatus completedStatus = ActivityStatus.Unknown;
        var idsToIndexCount = 0;
        var updateMs = 0L;

        logger.LogInformation(
            "{nameofIndexer} initiated. task-activity-context-instance-id: '{contextInstanceId}'. Pass: {indexerContextWrapperPass}.",
            nameof(Indexer), context.InstanceId, indexerContextWrapper.Pass);
        var indexerContext = indexerContextWrapper.IndexerContext;

        if (indexerContext.IndexIds == null)
        {
            throw new ArgumentException("IndexIds must be provided.");
        }

        if (indexerContext.IndexerPassOperationIds == null)
        {
            throw new ArgumentException("IndexerPassOperationIds must be provided.");
        }

        var passes = indexerContext.IndexerPassOperationIds.Length;
        if (indexerContextWrapper.Pass < 1 || indexerContextWrapper.Pass > passes)
        {
            throw new ArgumentException($"Pass must be between 1 and {passes}.");
        }

        logger.LogInformation("Pre: {indexerContext} {indexerOptions}", indexerContext.ToString(),
            _indexerOptions.ToString());
        var isPrimaryPass = indexingStrategy.IsPrimaryPass(indexerContextWrapper.Pass, passes);
        var indexingContext = _indexerOptions.ToIndexingContext() with
        {
            IndexSpotify = indexingStrategy.IndexSpotify(),
            SkipSpotifyUrlResolving = false,
            SkipYouTubeUrlResolving = !indexingStrategy.ResolveYouTube(),
            SkipExpensiveYouTubeQueries = !isPrimaryPass || !indexingStrategy.ExpensiveYouTubeQueries(),
            SkipExpensiveSpotifyQueries = !isPrimaryPass || !indexingStrategy.ExpensiveSpotifyQueries(),
            SkipPodcastDiscovery = true
        };

        var originalIndexingContext = indexerContext with { };

        logger.LogInformation("Post: {indexerContext}", indexerContext.ToString());

        if (!activityOptionsProvider.RunIndex(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Indexer), reason);
            return indexerContext with
            {
                Success = true,
                SkipYouTubeUrlResolving = indexerContext.SkipYouTubeUrlResolving,
                YouTubeError = false,
                SkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
                SpotifyError = false
            };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Indexer), reason);
        }

        var indexerOperationId = indexerContext.IndexerPassOperationIds[indexerContextWrapper.Pass - 1];
        initiatedStatus = await activityMarshaller.Initiate(indexerOperationId, nameof(Indexer));

        if (initiatedStatus != ActivityStatus.Initiated)
        {
            if (initiatedStatus == ActivityStatus.Failed)
            {
                return indexerContext with
                {
                    YouTubeError = true,
                    SpotifyError = true,
                    Success = false
                };
            }

            if (indexerContext.DuplicateIndexerPassOperations == null)
            {
                indexerContext = indexerContext with { DuplicateIndexerPassOperations = new bool[passes] };
            }

            indexerContext.DuplicateIndexerPassOperations[indexerContextWrapper.Pass - 1] = true;
        }

        bool results;
        try
        {
            var idsToIndex = indexerContext.IndexIds[indexerContextWrapper.Pass - 1];
            idsToIndexCount = idsToIndex.Length;

            var updateStopwatch = Stopwatch.StartNew();
            results = await podcastsUpdater.UpdatePodcasts(idsToIndex, indexingContext);
            updateStopwatch.Stop();
            updateMs = updateStopwatch.ElapsedMilliseconds;

            logger.LogWarning(
                "IndexerCostProbe.Update instance-id='{InstanceId}' pass='{Pass}' update-ms='{UpdateMs}'.",
                context.InstanceId,
                indexerContextWrapper.Pass,
                updateMs);
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            logger.LogError(ex,
                "Failure to execute {nameofIPodcastsUpdater}.{nameofIPodcastsUpdater.UpdatePodcasts}.",
                nameof(IPodcastsUpdater), nameof(IPodcastsUpdater.UpdatePodcasts));
            results = false;
        }
        finally
        {
            try
            {
                completedStatus = await activityMarshaller.Complete(indexerOperationId, nameof(Indexer));

                if (completedStatus != ActivityStatus.Completed)
                {
                    logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to complete activity.");
            }
        }

        if (!results)
        {
            logger.LogError("Failure occurred");
        }

        var result = indexerContext with
        {
            Success = results,
            SkipYouTubeUrlResolving = indexingContext.SkipYouTubeUrlResolving,
            YouTubeError = indexingContext.SkipYouTubeUrlResolving != originalIndexingContext.SkipYouTubeUrlResolving,
            SkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
            SpotifyError = indexingContext.SkipSpotifyUrlResolving != originalIndexingContext.SkipSpotifyUrlResolving
        };

        memoryProbe.End();

        logger.LogInformation("{nameofRunAsync} Completed. Pass: {indexerContextWrapperPass}. Result: {result}",
            nameof(RunAsync), indexerContextWrapper.Pass, result);

        return result;
    }
}