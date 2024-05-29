using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Discovery;

public class DiscoveryTrigger(ILogger<DiscoveryTrigger> logger)
{
    [Function("DiscoveryTrigger")]
    public async Task Run([TimerTrigger("15 11/12 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )] TimerInfo myTimer,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation($"{nameof(DiscoveryTrigger)} {nameof(Run)} initiated.");
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'. Status-Code: '{ex.StatusCode}', Status: '{ex.Status}'.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'.");
            throw;
        }

        logger.LogInformation($"{nameof(DiscoveryTrigger)} {nameof(Run)} complete. Instance-id= '{instanceId}'.");
    }
}