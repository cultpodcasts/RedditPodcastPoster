using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(HourlyOrchestration))]
public class HourlyOrchestration : TaskOrchestrator<object, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<HourlyOrchestration>();
        logger.LogInformation(
            $"{nameof(HourlyOrchestration)}.{nameof(RunAsync)} initiated. Instance-id: '{context.InstanceId}'.");


        var indexIds = await context.CallIndexIdProviderAsync(new object());
        logger.LogInformation($"{nameof(IndexIdProvider)} complete.");

        var indexerContext = new IndexerContext(context.NewGuid(), indexIds);
        indexerContext = await context.CallIndexerAsync(new IndexerContextWrapper(indexerContext, 1));
        logger.LogInformation($"{nameof(Indexer)} Pass 1 complete.");

        indexerContext = await context.CallIndexerAsync(new IndexerContextWrapper(indexerContext, 2));
        logger.LogInformation($"{nameof(Indexer)} Pass 2 complete.");

        indexerContext =
            await context.CallCategoriserAsync(indexerContext with {CategoriserOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Categoriser)} complete.");

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Poster)} complete.");

        indexerContext =
            await context.CallPublisherAsync(indexerContext with {PublisherOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Publisher)} complete.");

        indexerContext = await context.CallTweetAsync(indexerContext with {TweetOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Tweet)} complete. All tasks complete.");

        indexerContext = await context.CallBlueskyAsync(indexerContext with { BlueskyOperationId = context.NewGuid() });
        logger.LogInformation($"{nameof(Bluesky)} complete. All tasks complete.");

        return indexerContext;
    }
}