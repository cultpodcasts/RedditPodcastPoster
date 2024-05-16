using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Discovery;

[DurableTask(nameof(Orchestration))]
public class Orchestration : TaskOrchestrator<object, object>
{
    public override async Task<object> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>();
        logger.LogInformation(
            $"{nameof(Orchestration)}.{nameof(RunAsync)} initiated. Instance-id: '{context.InstanceId}'.");

        //var indexerContext = new IndexerContext(context.NewGuid());
        //indexerContext = await context.CallIndexerAsync(indexerContext);
        //logger.LogInformation($"{nameof(Indexer)} complete.");


        return new object();
    }
}