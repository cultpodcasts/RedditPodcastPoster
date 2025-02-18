using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Discovery;

public class DiscoveryTrigger(ILogger<DiscoveryTrigger> logger)
{
    [Function("DiscoveryTrigger")]
    public async Task Run([TimerTrigger("30 3/6 * * *" /* 30 3/6 * * * */
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo myTimer,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation("{nameofDiscoveryTrigger} {nameofRun} initiated.",
            nameof(DiscoveryTrigger), nameof(Run));
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                "Failure to execute '{nameofScheduleNewOrchestrationInstanceAsync}' for '{nameofOrchestration}'. Status-Code: '{StatusCode}', Status: '{Status}'.",
                nameof(client.ScheduleNewOrchestrationInstanceAsync), nameof(Orchestration), ex.StatusCode, ex.Status);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Failure to execute '{nameofScheduleNewOrchestrationInstanceAsync}' for '{nameofOrchestration}'.",
                nameof(client.ScheduleNewOrchestrationInstanceAsync), nameof(Orchestration));
            throw;
        }

        logger.LogInformation("{nameofDiscoveryTrigger} {nameofRun} complete. Instance-id= '{instanceId}'.",
            nameof(DiscoveryTrigger), nameof(Run), instanceId);
    }
}