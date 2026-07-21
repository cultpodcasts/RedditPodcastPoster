using Azure.Diagnostics;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

using RedditPodcastPoster.Models.Discovery;

using Discovery.Orchestrations;
using Discovery.Models;
using Discovery.Services;
using Discovery.Activities;

namespace Discovery.Triggers;

public class DiscoveryTrigger(
    ILogger<DiscoveryTrigger> logger,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    IDiscoveryScheduleProvider discoveryScheduleProvider)
{
    private static readonly TimeSpan InstanceLookback = TimeSpan.FromHours(36);
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function("DiscoveryTrigger")]
    public async Task Run([TimerTrigger("0 */30 * * * *"
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

        var scheduleConfig = await discoveryScheduleProvider.GetAsync(cancellationToken);
        if (!scheduleConfig.Enabled)
        {
            logger.LogWarning(
                "{DiscoveryTriggerName} {RunName} skipped. DiscoveryScheduleConfig.enabled=false.",
                nameof(DiscoveryTrigger),
                nameof(Run));
            memoryProbe.End();
            return;
        }

        IReadOnlyList<TimeOnly> runTimes;
        try
        {
            runTimes = DiscoverySchedule.ParseRunTimes(scheduleConfig.RunTimes);
        }
        catch (FormatException ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            logger.LogError(ex, "Invalid DiscoveryScheduleConfig.runTimes; skipping tick.");
            throw;
        }

        var ukTz = DiscoverySchedule.ResolveUkTimeZone(scheduleConfig.TimeZoneId);
        var dueSlot = DiscoverySchedule.TryMatchDueSlot(utcNow, runTimes, DiscoverySchedule.DefaultGrace, ukTz);
        if (dueSlot is null)
        {
            logger.LogInformation(
                "{DiscoveryTriggerName} {RunName} no-op. UK local time is not within grace of a scheduled runTimes slot.",
                nameof(DiscoveryTrigger),
                nameof(Run));
            memoryProbe.End();
            return;
        }

        var currentSlot = dueSlot.Value;
        var priorSlot = DiscoverySchedule.GetPriorSlot(currentSlot, runTimes, ukTz);

        logger.LogWarning(
            "{DiscoveryTriggerName} {RunName} initiated slot='{SlotId}' slot-utc='{SlotUtc}'.",
            nameof(DiscoveryTrigger),
            nameof(Run),
            currentSlot.SlotId,
            DiscoverySchedule.FormatSlot(currentSlot));

        var orchestrationInstances = await GetDiscoveryOrchestrationInstancesAsync(client, cancellationToken);
        LogDiscoverySlotAudit(DiscoverySlotAuditor.AuditSlot(priorSlot, priorSlot.SlotStartUtc, orchestrationInstances, runTimes, ukTz));

        LogDiscoveryOrchestrationHealthIssues(
            DiscoveryOrchestrationHealthChecker.FindFailedInstances(orchestrationInstances));

        var stalePendingInstances = await TerminateStalePendingInstancesAsync(
            client, currentSlot, orchestrationInstances, cancellationToken);
        if (stalePendingInstances.Count > 0)
        {
            orchestrationInstances = orchestrationInstances
                .Where(instance => !stalePendingInstances.Contains(instance))
                .ToList();
        }

        var currentSlotAudit = DiscoverySlotAuditor.AuditSlot(
            currentSlot, currentSlot.SlotStartUtc, orchestrationInstances, runTimes, ukTz);
        if (currentSlotAudit.Kind == DiscoverySlotAuditKind.Completed)
        {
            LogDiscoverySlotAudit(currentSlotAudit);
            logger.LogWarning(
                "{DiscoveryTriggerName} {RunName} skipped. Discovery already completed for slot='{SlotId}' instance-id='{InstanceId}'.",
                nameof(DiscoveryTrigger),
                nameof(Run),
                currentSlot.SlotId,
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
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(Orchestration),
                new DiscoveryOrchestrationRunInput(
                    utcNow,
                    currentSlot.SlotStartUtc,
                    currentSlot.SlotId,
                    runTimes.Select(t => t.ToString("HH\\:mm")).ToArray()));
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
            "{DiscoveryTriggerName} {RunName} scheduled slot='{SlotId}' instance-id='{InstanceId}'.",
            nameof(DiscoveryTrigger),
            nameof(Run),
            currentSlot.SlotId,
            instanceId);

        memoryProbe.End();
    }

    private async Task<IReadOnlyList<DiscoveryOrchestrationInstance>> TerminateStalePendingInstancesAsync(
        DurableTaskClient client,
        DiscoverySlot currentSlot,
        IReadOnlyList<DiscoveryOrchestrationInstance> orchestrationInstances,
        CancellationToken cancellationToken)
    {
        var staleInstances = DiscoveryOrchestrationHealthChecker.GetStalePendingInstances(
            currentSlot.SlotStartUtc, orchestrationInstances);
        foreach (var staleInstance in staleInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await client.TerminateInstanceAsync(
                    staleInstance.InstanceId,
                    $"Stale pending {nameof(Orchestration)} terminated by {nameof(DiscoveryTrigger)}.",
                    cancellationToken);
                logger.LogWarning(
                    "{DiscoveryTriggerName} terminated-stale instance-id='{InstanceId}' created-at-utc='{CreatedAtUtc:O}' slot='{SlotId}'.",
                    nameof(DiscoveryTrigger),
                    staleInstance.InstanceId,
                    staleInstance.CreatedAt.UtcDateTime,
                    currentSlot.SlotId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure to terminate stale pending '{OrchestrationName}' instance-id='{InstanceId}' created-at-utc='{CreatedAtUtc:O}'.",
                    nameof(Orchestration), staleInstance.InstanceId, staleInstance.CreatedAt.UtcDateTime);
            }
        }

        return staleInstances;
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
                "Discovery slot-audit kind='{Kind}' slot='{SlotId}' instance-id='{InstanceId}' status='{Status}'.",
                audit.Kind,
                audit.SlotId,
                audit.InstanceId,
                audit.Status);
            return;
        }

        logger.LogWarning(
            "Discovery slot-audit kind='{Kind}' slot='{SlotId}' instance-id='{InstanceId}' status='{Status}' created-at='{CreatedAtUtc}'. {Message}",
            audit.Kind,
            audit.SlotId,
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
