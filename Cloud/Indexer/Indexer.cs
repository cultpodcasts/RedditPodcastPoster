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
    IOptions<IndexerOptions> indexerOptions,
    ILogger<Indexer> logger)
    : TaskActivity<IndexerContextWrapper, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(
        TaskActivityContext context, IndexerContextWrapper indexerContextWrapper)
    {
        logger.LogInformation(
            $"{nameof(Indexer)} initiated. task-activity-context-instance-id: '{context.InstanceId}'. Pass: {indexerContextWrapper.Pass}.");
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

        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_indexerOptions.ToString());
        var indexingContext = _indexerOptions.ToIndexingContext() with
        {
            IndexSpotify = indexingStrategy.IndexSpotify(),
            SkipSpotifyUrlResolving = false,
            SkipYouTubeUrlResolving = !indexingStrategy.ResolveYouTube(),
            SkipExpensiveYouTubeQueries = !indexingStrategy.ExpensiveYouTubeQueries(),
            SkipExpensiveSpotifyQueries = !indexingStrategy.ExpensiveSpotifyQueries(),
            SkipPodcastDiscovery = true
        };

        var originalIndexingContext = indexerContext with { };

        logger.LogInformation(indexingContext.ToString());

        if (DryRun.IsIndexDryRun)
        {
            return indexerContext with
            {
                Success = true,
                SkipYouTubeUrlResolving = indexerContext.SkipYouTubeUrlResolving,
                YouTubeError = false,
                SkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
                SpotifyError = false
            };
        }

        var indexerOperationId = indexerContext.IndexerPassOperationIds[indexerContextWrapper.Pass - 1];
        var activityBooked = await activityMarshaller.Initiate(indexerOperationId, nameof(Indexer));
        if (activityBooked != ActivityStatus.Initiated)
        {
            if (activityBooked == ActivityStatus.Failed)
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
                indexerContext = indexerContext with {DuplicateIndexerPassOperations = new bool[passes]};
            }

            indexerContext.DuplicateIndexerPassOperations[indexerContextWrapper.Pass - 1] = true;
        }

        bool results;
        try
        {
            var idsToIndex = indexerContext.IndexIds[indexerContextWrapper.Pass - 1];
            results = await podcastsUpdater.UpdatePodcasts(idsToIndex, indexingContext);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to execute {nameof(IPodcastsUpdater)}.{nameof(IPodcastsUpdater.UpdatePodcasts)}.");
            results = false;
        }
        finally
        {
            try
            {
                activityBooked = await activityMarshaller.Complete(indexerOperationId, nameof(Indexer));
                if (activityBooked != ActivityStatus.Completed)
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

        logger.LogInformation($"{nameof(RunAsync)} Completed. Pass: {indexerContextWrapper.Pass}. Result: {result}");

        return result;
    }
}