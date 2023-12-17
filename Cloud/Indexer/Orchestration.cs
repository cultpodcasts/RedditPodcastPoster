using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Orchestration))]
public class Orchestration : TaskOrchestrator<object, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>(); // orchestrations do NOT have access to DI.
        logger.LogInformation(
            $"{nameof(Orchestration)}.{nameof(RunAsync)} initiated. Instance-id: '{context.InstanceId}'.");

        var indexerContext = new IndexerContext(){IndexerOperationId = context.NewGuid() };
        indexerContext = await context.CallIndexerAsync(indexerContext);
        logger.LogInformation($"{nameof(Indexer)} complete.");

        indexerContext =
            await context.CallCategoriserAsync(indexerContext with {CategoriserOperationId = context.NewGuid() });
        logger.LogInformation($"{nameof(Categoriser)} complete.");

        indexerContext = await context.CallPosterAsync(indexerContext with {PosterOperationId = context.NewGuid()});
        logger.LogInformation($"{nameof(Poster)} complete.");

        indexerContext = await context.CallPublisherAsync(indexerContext with {PublisherOperationId=context.NewGuid()});
        logger.LogInformation($"{nameof(Publisher)} complete.");

        indexerContext = await context.CallTweetAsync(indexerContext with {TweetOperationId=context.NewGuid()});
        logger.LogInformation($"{nameof(Tweet)} complete. All tasks complete.");

        return indexerContext;
    }
}