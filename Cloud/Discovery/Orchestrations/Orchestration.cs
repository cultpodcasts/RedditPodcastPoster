using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Discovery.Activities;
using Discovery.Models;
using Discovery.Services;

namespace Discovery.Orchestrations;

public class Orchestration
{
    [Function(nameof(Orchestration))]
    public static async Task<DiscoveryContext> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        DiscoveryOrchestrationRunInput? input)
    {
        var logger = context.CreateReplaySafeLogger<Orchestration>();
        logger.LogWarning(
            "{nameofOrchestration}.{nameofRun} initiated. Instance-id: '{contextInstanceId}'.",
            nameof(Orchestration), nameof(Run), context.InstanceId);

        // Stale-run guard: an instance scheduled for an earlier discovery slot (e.g. a Pending
        // backlog draining after a broken host was fixed) must no-op instead of re-running
        // discovery. Deterministic: input is fixed and CurrentUtcDateTime is replay-safe.
        // Instances scheduled by older code carry no input and run unguarded.
        if (input is not null)
        {
            var runTimes = DiscoverySchedule.ParseRunTimes(input.RunTimesUk);
            if (DiscoverySchedule.IsStaleRun(input.SlotStartUtc, context.CurrentUtcDateTime, runTimes))
            {
                logger.LogWarning(
                    "{nameofOrchestration} stale-run-skipped instance-id='{InstanceId}' slot='{SlotId}' scheduled-at-utc='{ScheduledAtUtc:O}' current-utc='{CurrentUtc:O}'.",
                    nameof(Orchestration), context.InstanceId, input.SlotId, input.ScheduledAtUtc,
                    context.CurrentUtcDateTime);
                return new DiscoveryContext(Guid.Empty, Success: true);
            }
        }

        var discoveryContext = new DiscoveryContext(context.NewGuid());
        logger.LogInformation("{nameofRun}: Pre: discovery-context: {discoveryContext}",
            nameof(Run), discoveryContext);
        var result = await context.CallActivityAsync<DiscoveryContext>(nameof(Discover), discoveryContext);
        if (result is not { Success: true } and not { DuplicateDiscoveryOperation: true })
        {
            throw new DiscoveryOrchestrationIncompleteException(
                $"Discover activity returned without success (operation-id='{discoveryContext.DiscoveryOperationId}', success='{result.Success}', duplicate='{result.DuplicateDiscoveryOperation}').");
        }

        logger.LogInformation("{nameofRun}: Post: discovery-context: {result}", nameof(Run), result);
        logger.LogWarning("{nameofDiscover} complete. Instance-id='{InstanceId}'.", nameof(Discover), context.InstanceId);


        return result;
    }
}
