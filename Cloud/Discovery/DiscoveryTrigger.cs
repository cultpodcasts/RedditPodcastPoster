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
    public async Task Run([TimerTrigger("0 33 2/6 * * *"
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
        var currentSlotStart = DiscoverySchedule.GetSlotStartForTime(utcNow);
        var priorSlotStart = DiscoverySchedule.GetPriorSlotStart(currentSlotStart);

        logger.LogWarning(
            "{DiscoveryTriggerName} {RunName} initiated slot-utc='{SlotUtc}'.",
            nameof(DiscoveryTrigger),
            nameof(Run),
            DiscoverySlotAuditor.FormatSlot(currentSlotStart));

        var orchestrationInstances = await GetDiscoveryOrchestrationInstancesAsync(client, cancellationToken);
        LogDiscoverySlotAudit(DiscoverySlotAuditor.AuditSlot(priorSlotStart, orchestrationInstances));

        LogDiscoveryOrchestrationHealthIssues(
            DiscoveryOrchestrationHealthChecker.FindFailedInstances(orchestrationInstances));

        var currentSlotAudit = DiscoverySlotAuditor.AuditSlot(currentSlotStart, orchestrationInstances);
        if (currentSlotAudit.Kind == DiscoverySlotAuditKind.Completed)
        {
            LogDiscoverySlotAudit(currentSlotAudit);
            logger.LogWarning(
                "{DiscoveryTriggerName} {RunName} skipped. Discovery already completed for slot-utc='{SlotUtc}' instance-id='{InstanceId}'.",
                nameof(DiscoveryTrigger),
                nameof(Run),
                DiscoverySlotAuditor.FormatSlot(currentSlotStart),
                currentSlotAudit.InstanceId);

            memoryProbe.End();
            return;
        }

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
            logger.LogWarning(
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

        logger.LogWarning(
            "{DiscoveryTriggerName} {RunName} scheduled slot-utc='{SlotUtc}' instance-id='{InstanceId}'.",
            nameof(DiscoveryTrigger),
            nameof(Run),
            DiscoverySlotAuditor.FormatSlot(currentSlotStart),
            instanceId);

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

    private void LogDiscoverySlotAudit(DiscoverySlotAudit audit)
    {
        if (audit.Kind == DiscoverySlotAuditKind.Missing)
        {
            var exception = new DiscoveryOrchestrationIncompleteException(audit.Message);
            logger.LogError(
                exception,
                "Discovery slot-audit kind='{Kind}' slot-utc='{SlotUtc}' instance-id='{InstanceId}' status='{Status}'.",
                audit.Kind,
                DiscoverySlotAuditor.FormatSlot(audit.SlotStartUtc),
                audit.InstanceId,
                audit.Status);
            return;
        }

        logger.LogWarning(
            "Discovery slot-audit kind='{Kind}' slot-utc='{SlotUtc}' instance-id='{InstanceId}' status='{Status}' created-at='{CreatedAtUtc}'. {Message}",
            audit.Kind,
            DiscoverySlotAuditor.FormatSlot(audit.SlotStartUtc),
            audit.InstanceId,
            audit.Status,
            audit.CreatedAt?.UtcDateTime,
            audit.Message);
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
