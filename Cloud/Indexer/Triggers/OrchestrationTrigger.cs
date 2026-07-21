using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Indexer.Orchestrations;
using Indexer.Models;

namespace Indexer.Triggers;

public class OrchestrationTrigger(ILogger<OrchestrationTrigger> logger)
{
    private static readonly TimeSpan OrchestrationDelay = TimeSpan.FromSeconds(10);

    [Function("Hourly")]
    public async Task RunHourly(
        [TimerTrigger("0 3 * * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var hourUtc = utcNow.Hour;
        logger.LogWarning(
            "OrchestrationTrigger RunHourly initiated hour-utc='{HourUtc}'.",
            hourUtc);

        var hourlyInstances = await GetHourlyOrchestrationInstancesAsync(client, cancellationToken);
        LogHourlyOrchestrationHealthIssues(HourlyOrchestrationHealthChecker.FindPriorHourIssues(utcNow, hourlyInstances));

        await TerminateStalePendingInstancesAsync(client, nameof(RunHourly), utcNow, hourlyInstances, cancellationToken);

        if (HourlyOrchestrationCatchUpEvaluator.HasActiveHourlyOrchestrationInCurrentUtcHour(utcNow, hourlyInstances))
        {
            logger.LogWarning(
                "{OrchestrationTriggerName} {RunHourlyName} skipped. '{HourlyOrchestrationName}' is already active for UTC hour {HourUtc}.",
                nameof(OrchestrationTrigger), nameof(RunHourly), nameof(HourlyOrchestration), hourUtc);
            return;
        }

        await ScheduleHourlyOrchestrationAsync(client, nameof(RunHourly), utcNow, cancellationToken);
    }

    [Function("HourlyCatchUp")]
    public async Task RunHourlyCatchUp(
        [TimerTrigger("0 8 * * * *"
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
        LogHourlyOrchestrationHealthIssues(HourlyOrchestrationHealthChecker.FindPriorHourIssues(utcNow, hourlyInstances));
        LogHourlyOrchestrationHealthIssues(
            HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(utcNow, hourlyInstances));

        await TerminateStalePendingInstancesAsync(client, nameof(RunHourlyCatchUp), utcNow, hourlyInstances,
            cancellationToken);

        if (!HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(utcNow, hourlyInstances, out var skipReason))
        {
            logger.LogInformation(
                "{OrchestrationTriggerName} {RunHourlyCatchUpName} skipped for UTC hour {HourUtc}. Reason='{SkipReason}'.",
                nameof(OrchestrationTrigger), nameof(RunHourlyCatchUp), utcNow.Hour, skipReason);
            return;
        }

        LogHourlyOrchestrationHealthIssues(
        [
            HourlyOrchestrationHealthChecker.CreateCurrentHourPrimaryMissedIssue(utcNow.Hour)
        ]);

        logger.LogWarning(
            "{OrchestrationTriggerName} {RunHourlyCatchUpName} scheduling missed hourly orchestration for UTC hour {HourUtc}.",
            nameof(OrchestrationTrigger), nameof(RunHourlyCatchUp), utcNow.Hour);

        await ScheduleHourlyOrchestrationAsync(client, nameof(RunHourlyCatchUp), utcNow, cancellationToken);
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
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hourUtc = utcNow.Hour;
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(HourlyOrchestration),
                new HourlyOrchestrationRunInput(utcNow));
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

        logger.LogWarning(
            "OrchestrationTrigger hourly-scheduled trigger='{TriggerName}' hour-utc='{HourUtc}' instance-id='{InstanceId}'.",
            triggerName, hourUtc, instanceId);
    }

    private async Task TerminateStalePendingInstancesAsync(
        DurableTaskClient client,
        string triggerName,
        DateTime utcNow,
        IReadOnlyList<HourlyOrchestrationInstance> hourlyInstances,
        CancellationToken cancellationToken)
    {
        var staleInstances = HourlyOrchestrationCatchUpEvaluator.GetStalePendingInstances(utcNow, hourlyInstances);
        foreach (var staleInstance in staleInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await client.TerminateInstanceAsync(
                    staleInstance.InstanceId,
                    $"Stale pending {nameof(HourlyOrchestration)} terminated by {triggerName}.",
                    cancellationToken);
                logger.LogWarning(
                    "OrchestrationTrigger hourly-terminated-stale trigger='{TriggerName}' instance-id='{InstanceId}' created-at-utc='{CreatedAtUtc:O}'.",
                    triggerName, staleInstance.InstanceId, staleInstance.CreatedAt.UtcDateTime);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure to terminate stale pending '{HourlyOrchestrationName}' instance-id='{InstanceId}' created-at-utc='{CreatedAtUtc:O}'.",
                    nameof(HourlyOrchestration), staleInstance.InstanceId, staleInstance.CreatedAt.UtcDateTime);
            }
        }
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
                OrchestrationRuntimeStatus.Running
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

            instances.Add(new HourlyOrchestrationInstance(
                metadata.CreatedAt,
                metadata.RuntimeStatus,
                metadata.InstanceId));
        }

        return instances;
    }

    private void LogHourlyOrchestrationHealthIssues(IReadOnlyList<HourlyOrchestrationHealthIssue> issues)
    {
        foreach (var issue in issues)
        {
            var exception = new HourlyOrchestrationIncompleteException(issue.Message);
            logger.LogError(
                exception,
                "Hourly orchestration health issue kind='{Kind}' hour-utc='{HourUtc}' instance-id='{InstanceId}' status='{Status}'.",
                issue.Kind,
                issue.HourUtc,
                issue.InstanceId,
                issue.Status);
        }
    }
}