using Azure.Diagnostics;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Discovery;

public class DiscoveryTrigger(
    ILogger<DiscoveryTrigger> logger,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
{
    private static readonly TimeSpan InstanceLookback = TimeSpan.FromHours(12);
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

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
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(DiscoveryTrigger));
        var utcNow = DateTime.UtcNow;

        logger.LogInformation("{nameofDiscoveryTrigger} {nameof(Run)} initiated.",
            nameof(DiscoveryTrigger), nameof(Run));

        var orchestrationInstances = await GetDiscoveryOrchestrationInstancesAsync(client, cancellationToken);
        LogDiscoveryOrchestrationHealthIssues(
            DiscoveryOrchestrationHealthChecker.FindFailedInstances(orchestrationInstances));

        var inProgressInstances = orchestrationInstances
            .Where(instance => DiscoveryOrchestrationHealthChecker.IsInProgressStatus(instance.Status))
            .ToList();

        var recentInProgress = inProgressInstances
            .Where(instance => utcNow - instance.CreatedAt.UtcDateTime
                < DiscoveryOrchestrationHealthChecker.CompletionThreshold)
            .ToList();

        if (recentInProgress.Count > 0)
        {
            var activeRun = recentInProgress[0];
            logger.LogInformation(
                "{DiscoveryTriggerName} {RunName} skipped. '{OrchestrationName}' instance-id='{InstanceId}' is still in progress (status='{Status}', created-at='{CreatedAtUtc}').",
                nameof(DiscoveryTrigger),
                nameof(Run),
                nameof(Orchestration),
                activeRun.InstanceId,
                activeRun.Status,
                activeRun.CreatedAt.UtcDateTime);

            memoryProbe.End();
            return;
        }

        foreach (var stuckInstance in inProgressInstances)
        {
            LogDiscoveryOrchestrationHealthIssues(
            [
                DiscoveryOrchestrationHealthChecker.CreateBlockedByActiveRunIssue(stuckInstance)
            ]);
        }

        if (inProgressInstances.Count > 0)
        {
            memoryProbe.End();
            return;
        }

        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        }
        catch (RpcException ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            var wrapped = new DiscoveryOrchestrationIncompleteException(
                "Discovery orchestration could not be scheduled.",
                ex);
            logger.LogError(
                wrapped,
                "Failure to execute '{ScheduleNewOrchestrationInstanceAsyncName}' for '{OrchestrationName}'. Status-Code: '{StatusCode}', Status: '{Status}'.",
                nameof(client.ScheduleNewOrchestrationInstanceAsync),
                nameof(Orchestration),
                ex.StatusCode,
                ex.Status);
            throw;
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            var wrapped = new DiscoveryOrchestrationIncompleteException(
                "Discovery orchestration could not be scheduled.",
                ex);
            logger.LogError(
                wrapped,
                "Failure to execute '{ScheduleNewOrchestrationInstanceAsyncName}' for '{OrchestrationName}'.",
                nameof(client.ScheduleNewOrchestrationInstanceAsync),
                nameof(Orchestration));
            throw;
        }

        logger.LogInformation("{nameofDiscoveryTrigger} {nameof(Run)} complete. Instance-id= '{instanceId}'.",
            nameof(DiscoveryTrigger), nameof(Run), instanceId);

        memoryProbe.End();
    }

    private static async Task<IReadOnlyList<DiscoveryOrchestrationInstance>> GetDiscoveryOrchestrationInstancesAsync(
        DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        var query = new OrchestrationQuery
        {
            CreatedFrom = DateTime.UtcNow.Subtract(InstanceLookback)
        };

        var instances = new List<DiscoveryOrchestrationInstance>();
        await foreach (var metadata in client.GetAllInstancesAsync(query))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (metadata.Name != new TaskName(nameof(Orchestration)))
            {
                continue;
            }

            instances.Add(new DiscoveryOrchestrationInstance(
                metadata.CreatedAt,
                metadata.RuntimeStatus,
                metadata.InstanceId));
        }

        return instances;
    }

    private void LogDiscoveryOrchestrationHealthIssues(IReadOnlyList<DiscoveryOrchestrationHealthIssue> issues)
    {
        foreach (var issue in issues)
        {
            var exception = new DiscoveryOrchestrationIncompleteException(issue.Message);
            logger.LogError(
                exception,
                "Discovery orchestration health issue kind='{Kind}' instance-id='{InstanceId}' status='{Status}' created-at='{CreatedAtUtc}'.",
                issue.Kind,
                issue.InstanceId,
                issue.Status,
                issue.CreatedAt.UtcDateTime);
        }
    }
}
