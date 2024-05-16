using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Discovery;

[DurableTask(nameof(Orchestration))]
public class Orchestration : TaskOrchestrator<object, DiscoveryContext>
{
    public override async Task<DiscoveryContext> RunAsync(TaskOrchestrationContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>();
        logger.LogInformation(
            $"{nameof(Orchestration)}.{nameof(RunAsync)} initiated. Instance-id: '{context.InstanceId}'.");

        var discoveryContext = new DiscoveryContext(context.NewGuid());
        //indexerContext = await context.CallIndexerAsync(indexerContext);
        logger.LogInformation($"{nameof(Discover)} complete.");


        return discoveryContext;
    }
}