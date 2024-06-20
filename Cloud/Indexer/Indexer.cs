using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Indexer;

[DurableTask(nameof(Indexer))]
public class Indexer(
    IPodcastsUpdater podcastsUpdater,
    IActivityMarshaller activityMarshaller,
    IIndexingStrategy indexingStrategy,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<Indexer> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(
        TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Indexer)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_indexerOptions.ToString());
        var indexingContext = _indexerOptions.ToIndexingContext() with
        {
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

        var activityBooked = await activityMarshaller.Initiate(indexerContext.IndexerOperationId, nameof(Indexer));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicateIndexerOperation = true
            };
        }

        bool results;
        try
        {
            results = await podcastsUpdater.UpdatePodcasts(indexingContext);
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
                activityBooked = await activityMarshaller.Complete(indexerContext.IndexerOperationId, nameof(Indexer));
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

        logger.LogInformation($"{nameof(RunAsync)} Completed. Result: {result}");

        return result;
    }
}