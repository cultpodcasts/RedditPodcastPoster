using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Orchestration))]
public class Orchestration : TaskOrchestrator<object, bool>
{
    public override async Task<bool> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>(); // orchestrations do NOT have access to DI.
        logger.LogInformation($"{nameof(Orchestration)}.{nameof(RunAsync)} initiated.");

        bool success= true;
        success = await context.CallIndexerAsync(new object());
        logger.LogInformation($"{nameof(Indexer)} complete.");

        success |= await context.CallCategoriserAsync(new object());
        logger.LogInformation($"{nameof(Categoriser)} complete.");

        success |= await context.CallPosterAsync(new object());
        logger.LogInformation($"{nameof(Poster)} complete.");

        success |= await context.CallPublisherAsync(new object());
        logger.LogInformation($"{nameof(Publisher)} complete.");

        success |= await context.CallTweetAsync(new object());
        logger.LogInformation($"{nameof(Tweet)} complete. All tasks complete.");

        return success;
    }
}