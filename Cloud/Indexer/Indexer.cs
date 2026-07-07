using System.Diagnostics;
using Azure;
using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace Indexer;

[DurableTask(nameof(Indexer))]
public class Indexer(
    IPodcastsUpdater podcastsUpdater,
    IActivityMarshaller activityMarshaller,
    IIndexingStrategy indexingStrategy,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    IYouTubeQuotaUsageTracker youTubeQuotaUsageTracker,
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
        var youTubeEnabledThisPass = indexingStrategy.ResolveYouTube();
        var indexingContext = _indexerOptions.ToIndexingContext() with
        {
            IndexSpotify = indexingStrategy.IndexSpotify(),
            SkipSpotifyUrlResolving = false,
            SkipYouTubeUrlResolving = !youTubeEnabledThisPass,
            SkipExpensiveYouTubeQueries = !isPrimaryPass || !indexingStrategy.ExpensiveYouTubeQueries(),
            SkipExpensiveSpotifyQueries = !isPrimaryPass || !indexingStrategy.ExpensiveSpotifyQueries(),
            SkipPodcastDiscovery = true
        };

        logger.LogInformation(
            "Indexer pass {Pass} indexing-context: {IndexingContext}",
            indexerContextWrapper.Pass, indexingContext.ToString());

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
        var idsToIndex = indexerContext.IndexIds[indexerContextWrapper.Pass - 1];
        idsToIndexCount = idsToIndex.Length;

        logger.LogWarning(
            "IndexerPassStart instance-id='{InstanceId}' pass='{Pass}' operation-id='{OperationId}' podcast-count='{PodcastCount}' youtube-enabled-pass='{YouTubeEnabledPass}' skip-youtube='{SkipYouTube}' skip-expensive-youtube='{SkipExpensiveYouTube}' skip-expensive-spotify='{SkipExpensiveSpotify}'",
            context.InstanceId,
            indexerContextWrapper.Pass,
            indexerOperationId,
            idsToIndexCount,
            youTubeEnabledThisPass,
            indexingContext.SkipYouTubeUrlResolving,
            indexingContext.SkipExpensiveYouTubeQueries,
            indexingContext.SkipExpensiveSpotifyQueries);

        if (youTubeEnabledThisPass)
        {
            try
            {
                await youTubeQuotaUsageTracker.EnsureHydratedAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to hydrate YouTube quota usage tracker at indexer pass start.");
            }
        }

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

        bool results = true;
        try
        {
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
            logger.LogError(ex,
                "Failure to execute {nameofIPodcastsUpdater}.{nameofIPodcastsUpdater.UpdatePodcasts}.",
                nameof(IPodcastsUpdater), nameof(IPodcastsUpdater.UpdatePodcasts));
            results = false;
        }
        finally
        {
            if (youTubeEnabledThisPass)
            {
                try
                {
                    await youTubeQuotaUsageTracker.FlushToCosmosAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure to flush YouTube quota usage to Cosmos.");
                }
            }

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
            logger.LogWarning(
                "Indexer pass {Pass} completed with one or more podcast update failures. Continuing orchestration.",
                indexerContextWrapper.Pass);
        }

        var result = indexerContext with
        {
            Success = results,
            SkipYouTubeUrlResolving = indexingContext.SkipYouTubeUrlResolving,
            YouTubeError = youTubeEnabledThisPass && indexingContext.SkipYouTubeUrlResolving,
            SkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
            SpotifyError = indexingContext.SkipSpotifyUrlResolving
        };

        memoryProbe.End(results);

        logger.LogWarning(
            "IndexerPassComplete instance-id='{InstanceId}' pass='{Pass}' operation-id='{OperationId}' podcast-count='{PodcastCount}' success='{Success}' skip-youtube='{SkipYouTube}' youtube-error='{YouTubeError}' skip-spotify='{SkipSpotify}' spotify-error='{SpotifyError}' update-ms='{UpdateMs}'",
            context.InstanceId,
            indexerContextWrapper.Pass,
            indexerOperationId,
            idsToIndexCount,
            result.Success,
            result.SkipYouTubeUrlResolving,
            result.YouTubeError,
            result.SkipSpotifyUrlResolving,
            result.SpotifyError,
            updateMs);

        logger.LogInformation("{nameofRunAsync} Completed. Pass: {indexerContextWrapperPass}. Result: {result}",
            nameof(RunAsync), indexerContextWrapper.Pass, result);

        return result;
    }
}