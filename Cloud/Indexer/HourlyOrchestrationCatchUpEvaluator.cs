using Microsoft.DurableTask.Client;

namespace Indexer;

public readonly record struct HourlyOrchestrationInstance(DateTimeOffset CreatedAt, OrchestrationRuntimeStatus Status);

public enum HourlyCatchUpSkipReason
{
    None,
    CompletedThisHour,
    InProgressThisHour,
    PendingThisHour
}

public static class HourlyOrchestrationCatchUpEvaluator
{
    private static readonly OrchestrationRuntimeStatus[] InProgressStatuses =
    [
        OrchestrationRuntimeStatus.Running,
        OrchestrationRuntimeStatus.ContinuedAsNew
    ];

    private static readonly OrchestrationRuntimeStatus[] FinishedStatuses =
    [
        OrchestrationRuntimeStatus.Completed,
        OrchestrationRuntimeStatus.Failed,
        OrchestrationRuntimeStatus.Terminated
    ];

    /// <summary>
    /// Pending instances older than this are treated as ghost schedules from a recycled worker
    /// (timer fired, durable instance created, host died before RunAsync started).
    /// </summary>
    public static TimeSpan StalePendingThreshold { get; set; } = TimeSpan.FromMinutes(3);

    public static DateTimeOffset GetUtcHourStart(DateTime utcNow) =>
        new(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, TimeSpan.Zero);

    public static bool IsInUtcHour(DateTimeOffset createdAt, DateTime utcNow)
    {
        var hourStart = GetUtcHourStart(utcNow);
        var hourEnd = hourStart.AddHours(1);
        return createdAt >= hourStart && createdAt < hourEnd;
    }

    public static bool HasHourlyOrchestrationForUtcHour(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances) =>
        instances.Any(instance => IsInUtcHour(instance.CreatedAt, utcNow));

    public static bool IsInProgressStatus(OrchestrationRuntimeStatus status) =>
        InProgressStatuses.Contains(status);

    public static bool HasActiveHourlyOrchestrationInCurrentUtcHour(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances) =>
        instances.Any(instance =>
            IsInUtcHour(instance.CreatedAt, utcNow) && InProgressStatuses.Contains(instance.Status));

    public static bool ShouldScheduleCatchUp(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances,
        out HourlyCatchUpSkipReason skipReason)
    {
        skipReason = HourlyCatchUpSkipReason.None;
        var instancesThisHour = instances
            .Where(instance => IsInUtcHour(instance.CreatedAt, utcNow))
            .ToList();

        if (instancesThisHour.Count == 0)
        {
            return true;
        }

        if (instancesThisHour.Any(instance => FinishedStatuses.Contains(instance.Status)))
        {
            skipReason = HourlyCatchUpSkipReason.CompletedThisHour;
            return false;
        }

        if (instancesThisHour.Any(instance => InProgressStatuses.Contains(instance.Status)))
        {
            skipReason = HourlyCatchUpSkipReason.InProgressThisHour;
            return false;
        }

        var pendingInstances = instancesThisHour
            .Where(instance => instance.Status == OrchestrationRuntimeStatus.Pending)
            .ToList();

        if (pendingInstances.Count == 0)
        {
            return true;
        }

        if (pendingInstances.All(instance => utcNow - instance.CreatedAt.UtcDateTime >= StalePendingThreshold))
        {
            return true;
        }

        skipReason = HourlyCatchUpSkipReason.PendingThisHour;
        return false;
    }

    public static bool ShouldScheduleCatchUp(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances) =>
        ShouldScheduleCatchUp(utcNow, instances, out _);
}
