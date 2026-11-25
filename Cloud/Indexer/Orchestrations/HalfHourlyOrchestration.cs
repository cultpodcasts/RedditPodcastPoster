using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Indexer.Activities;
using Indexer.Models;

namespace Indexer.Orchestrations;

public class HalfHourlyOrchestration
{
    [Function(nameof(HalfHourlyOrchestration))]
    public static async Task<IndexerContext> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        object? input)
    {
        var logger = context.CreateReplaySafeLogger<HalfHourlyOrchestration>();
        logger.LogInformation(
            "{nameofHalfHourlyOrchestration}.{nameofRun} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(HalfHourlyOrchestration), nameof(Run), context.InstanceId);

        var indexerContext = new IndexerContext
        {
            // hold back posting anything not ready
            SkipYouTubeUrlResolving = true,
            YouTubeError = true,
            SkipSpotifyUrlResolving = true,
            SpotifyError = true
        };

        indexerContext = await context.CallActivityAsync<IndexerContext>(nameof(LoadRecentCandidates), indexerContext);
        logger.LogInformation("{nameofLoadRecentCandidates} complete.", nameof(LoadRecentCandidates));

        indexerContext = await context.CallActivityAsync<IndexerContext>(
            nameof(Poster), indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPoster} complete.", nameof(Poster));

        indexerContext = await context.CallActivityAsync<IndexerContext>(
            nameof(Publisher), indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPublisher} complete.", nameof(Publisher));

        indexerContext = await context.CallActivityAsync<IndexerContext>(
            nameof(Bluesky), indexerContext with {BlueskyOperationId = context.NewGuid()});
        logger.LogInformation("{nameofBluesky} complete. All tasks complete.", nameof(Bluesky));

        return indexerContext;
    }
}
