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
            "{nameofOrchestration}.{nameofRunAsync} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(Orchestration), nameof(RunAsync), context.InstanceId);

        var discoveryContext = new DiscoveryContext(context.NewGuid());
        logger.LogInformation("{nameofRunAsync}: Pre: discovery-context: {discoveryContext}",
            nameof(RunAsync), discoveryContext);
        var result = await context.CallDiscoverAsync(discoveryContext);
        logger.LogInformation("{nameofRunAsync}: Post: discovery-context: {result}", nameof(RunAsync), result);
        logger.LogInformation("{nameofDiscover} complete.", nameof(Discover));


        return discoveryContext;
    }
}