using Microsoft.DurableTask.Client;
using Discovery.Models;
using Discovery.Activities;
using Discovery.Services;

namespace Discovery.Orchestrations;

public enum DiscoverySlotAuditKind
{
    Completed,
    Failed,
    InProgress,
    Missing
}

public readonly record struct DiscoverySlotAudit(
    DiscoverySlotAuditKind Kind,
    string SlotId,
    DateTimeOffset SlotStartUtc,
    string InstanceId,
    OrchestrationRuntimeStatus? Status,
    DateTimeOffset? CreatedAt,
    string Message);

public static class DiscoverySlotAuditor
{
    public static bool InstanceBelongsToSlot(
        DiscoveryOrchestrationInstance instance,
        DateTimeOffset slotStartUtc,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null)
    {
        var instanceSlot = DiscoverySchedule.GetLatestDueSlot(
            instance.CreatedAt.UtcDateTime, runTimesUk, ukTimeZone);
        return instanceSlot.SlotStartUtc == slotStartUtc;
    }

    public static DiscoverySlotAudit AuditSlot(
        DiscoverySlot slotHint,
        DateTimeOffset slotStartUtc,
        IEnumerable<DiscoveryOrchestrationInstance> instances,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null)
    {
        var slotId = slotHint.SlotStartUtc == slotStartUtc
            ? slotHint.SlotId
            : DiscoverySchedule.GetLatestDueSlot(slotStartUtc.UtcDateTime, runTimesUk, ukTimeZone).SlotId;

        var slotInstances = instances
            .Where(instance => InstanceBelongsToSlot(instance, slotStartUtc, runTimesUk, ukTimeZone))
            .OrderBy(instance => instance.CreatedAt)
            .ToList();

        if (slotInstances.Count == 0)
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Missing,
                slotId,
                slotStartUtc,
                string.Empty,
                null,
                null,
                $"No Discovery orchestration instance recorded for slot {slotId}.");
        }

        var completed = slotInstances
            .LastOrDefault(instance => instance.Status == OrchestrationRuntimeStatus.Completed);
        if (completed.InstanceId is { Length: > 0 })
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Completed,
                slotId,
                slotStartUtc,
                completed.InstanceId,
                completed.Status,
                completed.CreatedAt,
                $"Discovery slot {slotId} completed (instance-id='{completed.InstanceId}', created-at='{completed.CreatedAt.UtcDateTime:O}').");
        }

        var failed = slotInstances
            .LastOrDefault(instance => instance.Status == OrchestrationRuntimeStatus.Failed);
        if (failed.InstanceId is { Length: > 0 })
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Failed,
                slotId,
                slotStartUtc,
                failed.InstanceId,
                failed.Status,
                failed.CreatedAt,
                $"Discovery slot {slotId} failed (instance-id='{failed.InstanceId}', created-at='{failed.CreatedAt.UtcDateTime:O}').");
        }

        var inProgress = slotInstances[^1];
        return new DiscoverySlotAudit(
            DiscoverySlotAuditKind.InProgress,
            slotId,
            slotStartUtc,
            inProgress.InstanceId,
            inProgress.Status,
            inProgress.CreatedAt,
            $"Discovery slot {slotId} is still '{inProgress.Status}' (instance-id='{inProgress.InstanceId}', created-at='{inProgress.CreatedAt.UtcDateTime:O}').");
    }
}
