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
        if (result is not { Success: true } and not { DuplicateDiscoveryOperation: true })
        {
            throw new DiscoveryOrchestrationIncompleteException(
                $"Discover activity returned without success (operation-id='{discoveryContext.DiscoveryOperationId}', success='{result.Success}', duplicate='{result.DuplicateDiscoveryOperation}').");
        }

        logger.LogInformation("{nameofRunAsync}: Post: discovery-context: {result}", nameof(RunAsync), result);
        logger.LogInformation("{nameofDiscover} complete.", nameof(Discover));


        return result;
    }
}