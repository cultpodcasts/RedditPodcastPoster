using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Orchestration))]
public class Orchestration : TaskOrchestrator<object, IndexerResponse>
{
    public override async Task<IndexerResponse> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>(); // orchestrations do NOT have access to DI.
        logger.LogInformation($"{nameof(Orchestration)}.{nameof(RunAsync)} initiated.");

        IndexerResponse response;
        response = await context.CallIndexerAsync(new object());
        logger.LogInformation($"{nameof(Indexer)} complete.");

        response = await context.CallCategoriserAsync(response);
        logger.LogInformation($"{nameof(Categoriser)} complete.");

        response = await context.CallPosterAsync(response);
        logger.LogInformation($"{nameof(Poster)} complete.");

        response = await context.CallPublisherAsync(response);
        logger.LogInformation($"{nameof(Publisher)} complete.");

        response = await context.CallTweetAsync(response);
        logger.LogInformation($"{nameof(Tweet)} complete. All tasks complete.");

        return response;
    }
}