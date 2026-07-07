using Microsoft.DurableTask.Client;

namespace Discovery;

public enum DiscoverySlotAuditKind
{
    Completed,
    Failed,
    InProgress,
    Missing
}

public readonly record struct DiscoverySlotAudit(
    DiscoverySlotAuditKind Kind,
    DateTimeOffset SlotStartUtc,
    string InstanceId,
    OrchestrationRuntimeStatus? Status,
    DateTimeOffset? CreatedAt,
    string Message);

public static class DiscoverySlotAuditor
{
    public static bool InstanceBelongsToSlot(
        DiscoveryOrchestrationInstance instance,
        DateTimeOffset slotStartUtc) =>
        DiscoverySchedule.GetSlotStartForTime(instance.CreatedAt.UtcDateTime) == slotStartUtc;

    public static DiscoverySlotAudit AuditSlot(
        DateTimeOffset slotStartUtc,
        IEnumerable<DiscoveryOrchestrationInstance> instances)
    {
        var slotInstances = instances
            .Where(instance => InstanceBelongsToSlot(instance, slotStartUtc))
            .OrderBy(instance => instance.CreatedAt)
            .ToList();

        if (slotInstances.Count == 0)
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Missing,
                slotStartUtc,
                string.Empty,
                null,
                null,
                $"No Discovery orchestration instance recorded for slot {FormatSlot(slotStartUtc)}.");
        }

        var completed = slotInstances
            .LastOrDefault(instance => instance.Status == OrchestrationRuntimeStatus.Completed);
        if (completed.InstanceId is { Length: > 0 })
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Completed,
                slotStartUtc,
                completed.InstanceId,
                completed.Status,
                completed.CreatedAt,
                $"Discovery slot {FormatSlot(slotStartUtc)} completed (instance-id='{completed.InstanceId}', created-at='{completed.CreatedAt.UtcDateTime:O}').");
        }

        var failed = slotInstances
            .LastOrDefault(instance => instance.Status == OrchestrationRuntimeStatus.Failed);
        if (failed.InstanceId is { Length: > 0 })
        {
            return new DiscoverySlotAudit(
                DiscoverySlotAuditKind.Failed,
                slotStartUtc,
                failed.InstanceId,
                failed.Status,
                failed.CreatedAt,
                $"Discovery slot {FormatSlot(slotStartUtc)} failed (instance-id='{failed.InstanceId}', created-at='{failed.CreatedAt.UtcDateTime:O}').");
        }

        var inProgress = slotInstances[^1];
        return new DiscoverySlotAudit(
            DiscoverySlotAuditKind.InProgress,
            slotStartUtc,
            inProgress.InstanceId,
            inProgress.Status,
            inProgress.CreatedAt,
            $"Discovery slot {FormatSlot(slotStartUtc)} is still '{inProgress.Status}' (instance-id='{inProgress.InstanceId}', created-at='{inProgress.CreatedAt.UtcDateTime:O}').");
    }

    public static string FormatSlot(DateTimeOffset slotStartUtc) =>
        $"{slotStartUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC";
}
