using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(HalfHourlyOrchestration))]
public class HalfHourlyOrchestration : TaskOrchestrator<object, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<HalfHourlyOrchestration>();
        logger.LogInformation(
            "{nameofHalfHourlyOrchestration}.{nameofRunAsync} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(HalfHourlyOrchestration), nameof(RunAsync), context.InstanceId);

        var indexerContext = new IndexerContext
        {
            // hold back posting anything not ready
            SkipYouTubeUrlResolving = true,
            YouTubeError = true,
            SkipSpotifyUrlResolving = true,
            SpotifyError = true
        };

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPoster} complete.", nameof(Poster));

        indexerContext =
            await context.CallPublisherAsync(indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation("{nameofPublisher} complete.", nameof(Publisher));

        indexerContext = await context.CallBlueskyAsync(indexerContext with {BlueskyOperationId = context.NewGuid()});
        logger.LogInformation("{nameofBluesky} complete. All tasks complete.", nameof(Bluesky));

        return indexerContext;
    }
}