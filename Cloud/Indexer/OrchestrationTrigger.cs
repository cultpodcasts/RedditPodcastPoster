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

        await ScheduleHourlyOrchestrationAsync(client, nameof(RunHourly), cancellationToken);
    }

    [Function("HourlyCatchUp")]
    public async Task RunHourlyCatchUp(
        [TimerTrigger("0 5 * * * *"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("{OrchestrationTriggerName} {RunHourlyCatchUpName} initiated.",
            nameof(OrchestrationTrigger), nameof(RunHourlyCatchUp));

        var utcNow = DateTime.UtcNow;
        var hourlyInstances = await GetHourlyOrchestrationInstancesAsync(client, cancellationToken);

        if (!HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(utcNow, hourlyInstances))
        {
            logger.LogInformation(
                "{OrchestrationTriggerName} {RunHourlyCatchUpName} skipped. Hourly orchestration already ran or is active for UTC hour {HourUtc}.",
                nameof(OrchestrationTrigger), nameof(RunHourlyCatchUp), utcNow.Hour);
            return;
        }

        logger.LogInformation(
            "{OrchestrationTriggerName} {RunHourlyCatchUpName} scheduling missed hourly orchestration for UTC hour {HourUtc}.",
            nameof(OrchestrationTrigger), nameof(RunHourlyCatchUp), utcNow.Hour);

        await ScheduleHourlyOrchestrationAsync(client, nameof(RunHourlyCatchUp), cancellationToken);
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

    private async Task ScheduleHourlyOrchestrationAsync(
        DurableTaskClient client,
        string triggerName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            "{OrchestrationTriggerName} {TriggerName} complete. Instance-id= '{InstanceId}'.",
            nameof(OrchestrationTrigger), triggerName, instanceId);
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

    private static async Task<IReadOnlyList<HourlyOrchestrationInstance>> GetHourlyOrchestrationInstancesAsync(
        DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        var query = new OrchestrationQuery
        {
            CreatedFrom = DateTime.UtcNow.Subtract(TimeSpan.FromHours(2))
        };

        var instances = new List<HourlyOrchestrationInstance>();
        await foreach (var metadata in client.GetAllInstancesAsync(query))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (metadata.Name != new TaskName(nameof(HourlyOrchestration)))
            {
                continue;
            }

            instances.Add(new HourlyOrchestrationInstance(metadata.CreatedAt, metadata.RuntimeStatus));
        }

        return instances;
    }
}