using Microsoft.DurableTask.Client;

namespace Discovery;

public readonly record struct DiscoveryOrchestrationInstance(
    DateTimeOffset CreatedAt,
    OrchestrationRuntimeStatus Status,
    string InstanceId = "");

public enum DiscoveryOrchestrationHealthIssueKind
{
    Failed,
    StuckInProgress,
    BlockedByActiveRun
}

public readonly record struct DiscoveryOrchestrationHealthIssue(
    DiscoveryOrchestrationHealthIssueKind Kind,
    string InstanceId,
    OrchestrationRuntimeStatus Status,
    DateTimeOffset CreatedAt,
    string Message);

public static class DiscoveryOrchestrationHealthChecker
{
    /// <summary>
    /// Discovery runs normally complete within a few minutes.
    /// </summary>
    public static TimeSpan CompletionThreshold { get; set; } = TimeSpan.FromMinutes(10);

    private static readonly OrchestrationRuntimeStatus[] InProgressStatuses =
    [
        OrchestrationRuntimeStatus.Running,
        OrchestrationRuntimeStatus.ContinuedAsNew,
        OrchestrationRuntimeStatus.Pending
    ];

    public static bool IsInProgressStatus(OrchestrationRuntimeStatus status) =>
        InProgressStatuses.Contains(status);

    public static IReadOnlyList<DiscoveryOrchestrationHealthIssue> FindFailedInstances(
        IEnumerable<DiscoveryOrchestrationInstance> instances) =>
        instances
            .Where(instance => instance.Status == OrchestrationRuntimeStatus.Failed)
            .Select(instance => new DiscoveryOrchestrationHealthIssue(
                DiscoveryOrchestrationHealthIssueKind.Failed,
                instance.InstanceId,
                instance.Status,
                instance.CreatedAt,
                $"Discovery orchestration failed (instance-id='{instance.InstanceId}', created-at='{instance.CreatedAt.UtcDateTime:O}')."))
            .ToList();

    public static IReadOnlyList<DiscoveryOrchestrationHealthIssue> FindIncompleteInstances(
        DateTime utcNow,
        IEnumerable<DiscoveryOrchestrationInstance> instances)
    {
        var issues = new List<DiscoveryOrchestrationHealthIssue>();
        issues.AddRange(FindFailedInstances(instances));

        foreach (var instance in instances.Where(instance => IsInProgressStatus(instance.Status)))
        {
            var age = utcNow - instance.CreatedAt.UtcDateTime;
            if (age < CompletionThreshold)
            {
                continue;
            }

            issues.Add(new DiscoveryOrchestrationHealthIssue(
                DiscoveryOrchestrationHealthIssueKind.StuckInProgress,
                instance.InstanceId,
                instance.Status,
                instance.CreatedAt,
                $"Discovery orchestration has not completed after {age.TotalMinutes:F0} minutes (status='{instance.Status}', instance-id='{instance.InstanceId}', created-at='{instance.CreatedAt.UtcDateTime:O}')."));
        }

        return issues;
    }

    public static DiscoveryOrchestrationHealthIssue CreateBlockedByActiveRunIssue(
        DiscoveryOrchestrationInstance instance) =>
        new(
            DiscoveryOrchestrationHealthIssueKind.BlockedByActiveRun,
            instance.InstanceId,
            instance.Status,
            instance.CreatedAt,
            $"Discovery trigger skipped scheduling because orchestration is still active (status='{instance.Status}', instance-id='{instance.InstanceId}', created-at='{instance.CreatedAt.UtcDateTime:O}').");
}
