using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class OrchestrationTrigger(ILogger<OrchestrationTrigger> logger)
{
    private static readonly TimeSpan OrchestrationDelay = TimeSpan.FromSeconds(10);

    [Function("Hourly")]
    public async Task RunHourly(
        [TimerTrigger("0 */1 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(RunHourly)} initiated.");

        if (await HasActiveOrchestrationInstanceAsync(client, nameof(HourlyOrchestration), cancellationToken))
        {
            logger.LogWarning(
                "{OrchestrationTriggerName} {RunHourlyName} skipped. Existing '{HourlyOrchestrationName}' instance is still active.",
                nameof(OrchestrationTrigger), nameof(RunHourly), nameof(HourlyOrchestration));
            return;
        }

        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HourlyOrchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                "Failure to execute '{ScheduleNewOrchestrationInstanceAsyncName}' for '{HourlyOrchestrationName}'. Status-Code: '{ExStatusCode}', Status: '{ExStatus}'.", nameof(client.ScheduleNewOrchestrationInstanceAsync), nameof(HourlyOrchestration), ex.StatusCode, ex.Status);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HourlyOrchestration)}'.");
            throw;
        }

        logger.LogInformation(
            "{OrchestrationTriggerName} {RunHourlyName} complete. Instance-id= '{InstanceId}'.", nameof(OrchestrationTrigger), nameof(RunHourly), instanceId);
    }


    [Function("HalfHourly")]
    public async Task RunHalfHourly(
        [TimerTrigger("30 */1 * * *"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(RunHalfHourly)} initiated.");

        if (await HasActiveOrchestrationInstanceAsync(client, nameof(HalfHourlyOrchestration), cancellationToken))
        {
            logger.LogWarning(
                "{OrchestrationTriggerName} {RunHalfHourlyName} skipped. Existing '{HalfHourlyOrchestrationName}' instance is still active.",
                nameof(OrchestrationTrigger), nameof(RunHalfHourly), nameof(HalfHourlyOrchestration));
            return;
        }

        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HalfHourlyOrchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                "Failure to execute '{ScheduleNewOrchestrationInstanceAsyncName}' for '{HalfHourlyOrchestrationName}'. Status-Code: '{ExStatusCode}', Status: '{ExStatus}'.", nameof(client.ScheduleNewOrchestrationInstanceAsync), nameof(HalfHourlyOrchestration), ex.StatusCode, ex.Status);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HalfHourlyOrchestration)}'.");
            throw;
        }

        logger.LogInformation(
            "{OrchestrationTriggerName} {RunHalfHourlyName} complete. Instance-id= '{InstanceId}'.", nameof(OrchestrationTrigger), nameof(RunHalfHourly), instanceId);
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