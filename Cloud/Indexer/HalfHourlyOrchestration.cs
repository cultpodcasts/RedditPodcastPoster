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
            $"{nameof(HalfHourlyOrchestration)}.{nameof(RunAsync)} initiated. Instance-id: '{context.InstanceId}'.");

        var indexerContext = new IndexerContext(context.NewGuid())
        {
            // hold back posting anything not ready
            SkipYouTubeUrlResolving = true,
            YouTubeError = true,
            SkipSpotifyUrlResolving = true,
            SpotifyError = true
        };

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Poster)} complete.");

        indexerContext =
            await context.CallPublisherAsync(indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Publisher)} complete.");

        indexerContext = await context.CallBlueskyAsync(indexerContext with { BlueskyOperationId = context.NewGuid() });
        logger.LogInformation($"{nameof(Bluesky)} complete. All tasks complete.");

        return indexerContext;
    }
}