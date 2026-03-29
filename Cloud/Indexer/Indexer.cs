using System.Diagnostics;
using Azure;
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
    ILogger<Indexer> logger)
    : TaskActivity<IndexerContextWrapper, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(
        TaskActivityContext context, IndexerContextWrapper indexerContextWrapper)
    {
        var runStopwatch = Stopwatch.StartNew();
        var instrumentationEnabled = _indexerOptions.EnableCostInstrumentation;
        ActivityStatus initiatedStatus = ActivityStatus.Unknown;
        ActivityStatus completedStatus = ActivityStatus.Unknown;
        var initiateMs = 0L;
        var updateMs = 0L;
        var completeMs = 0L;
        var idsToIndexCount = 0;

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
        var initiateStopwatch = Stopwatch.StartNew();
        initiatedStatus = await activityMarshaller.Initiate(indexerOperationId, nameof(Indexer));
        initiateStopwatch.Stop();
        initiateMs = initiateStopwatch.ElapsedMilliseconds;
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

            if (instrumentationEnabled)
            {
                logger.LogWarning(
                    "IndexerCostProbe.Start instance-id='{InstanceId}' pass='{Pass}' ids-to-index='{IdsToIndexCount}' index-spotify='{IndexSpotify}' skip-youtube-url-resolving='{SkipYouTubeUrlResolving}' skip-expensive-youtube-queries='{SkipExpensiveYouTubeQueries}' skip-expensive-spotify-queries='{SkipExpensiveSpotifyQueries}'.",
                    context.InstanceId,
                    indexerContextWrapper.Pass,
                    idsToIndexCount,
                    indexingContext.IndexSpotify,
                    indexingContext.SkipYouTubeUrlResolving,
                    indexingContext.SkipExpensiveYouTubeQueries,
                    indexingContext.SkipExpensiveSpotifyQueries);
            }

            var updateStopwatch = Stopwatch.StartNew();
            results = await podcastsUpdater.UpdatePodcasts(idsToIndex, indexingContext);
            updateStopwatch.Stop();
            updateMs = updateStopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {nameofIPodcastsUpdater}.{nameofIPodcastsUpdater.UpdatePodcasts}.",
                nameof(IPodcastsUpdater), nameof(IPodcastsUpdater.UpdatePodcasts));
            results = false;
        }
        finally
        {
            try
            {
                var completeStopwatch = Stopwatch.StartNew();
                completedStatus = await activityMarshaller.Complete(indexerOperationId, nameof(Indexer));
                completeStopwatch.Stop();
                completeMs = completeStopwatch.ElapsedMilliseconds;

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

        runStopwatch.Stop();

        if (instrumentationEnabled)
        {
            logger.LogWarning(
                "IndexerCostProbe.Complete instance-id='{InstanceId}' pass='{Pass}' success='{Success}' ids-to-index='{IdsToIndexCount}' initiate-status='{InitiatedStatus}' complete-status='{CompletedStatus}' initiate-ms='{InitiateMs}' update-ms='{UpdateMs}' complete-ms='{CompleteMs}' total-ms='{TotalMs}'.",
                context.InstanceId,
                indexerContextWrapper.Pass,
                result.Success,
                idsToIndexCount,
                initiatedStatus,
                completedStatus,
                initiateMs,
                updateMs,
                completeMs,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("{nameofRunAsync} Completed. Pass: {indexerContextWrapperPass}. Result: {result}",
            nameof(RunAsync), indexerContextWrapper.Pass, result);

        return result;
    }
}