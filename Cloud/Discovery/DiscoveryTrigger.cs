using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Discovery;

public class DiscoveryTrigger(ILogger<DiscoveryTrigger> logger)
{
    [Function("DiscoveryTrigger")]
    public async Task Run([TimerTrigger("30 2/6 * * *" /* 30 3/6 * * * */
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo myTimer,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("{nameofDiscoveryTrigger} {nameofRun} initiated.",
            nameof(DiscoveryTrigger), nameof(Run));

        if (await HasActiveOrchestrationInstanceAsync(client, nameof(Orchestration), cancellationToken))
        {
            logger.LogWarning(
                "{nameofDiscoveryTrigger} {nameofRun} skipped. Existing '{nameofOrchestration}' instance is still active.",
                nameof(DiscoveryTrigger), nameof(Run), nameof(Orchestration));
            return;
        }

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

    private static async Task<bool> HasActiveOrchestrationInstanceAsync(
        DurableTaskClient client,
        string orchestrationName,
        CancellationToken cancellationToken)
    {
        var query = new OrchestrationQuery
        {
            CreatedFrom = DateTime.UtcNow.Subtract(TimeSpan.FromDays(2)),
            Statuses =
            [
                OrchestrationRuntimeStatus.Pending,
                OrchestrationRuntimeStatus.Running,
                OrchestrationRuntimeStatus.ContinuedAsNew
            ]
        };

        await foreach (var metadata in client.GetAllInstancesAsync(query))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (metadata.Name == new TaskName(orchestrationName))
            {
                return true;
            }
        }

        return false;
    }
}