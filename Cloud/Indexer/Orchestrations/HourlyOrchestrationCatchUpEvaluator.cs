using Microsoft.DurableTask.Client;

using Indexer.Models;
using Indexer.Activities;

namespace Indexer.Orchestrations;

public readonly record struct HourlyOrchestrationInstance(
    DateTimeOffset CreatedAt,
    OrchestrationRuntimeStatus Status,
    string InstanceId = "");

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
        OrchestrationRuntimeStatus.Running
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

    /// <summary>
    /// Pending instances that should be terminated rather than left runnable: instances created in an
    /// earlier UTC hour (their slot has passed) or ghost schedules in the current hour older than
    /// <see cref="StalePendingThreshold" />. Left alone, such instances all execute when a healthy host
    /// next starts, producing duplicate hourly runs (Jul 2026 duplicate Bluesky posts incident).
    /// </summary>
    public static IReadOnlyList<HourlyOrchestrationInstance> GetStalePendingInstances(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances)
    {
        var hourStart = GetUtcHourStart(utcNow);
        return instances
            .Where(instance => instance.Status == OrchestrationRuntimeStatus.Pending)
            .Where(instance =>
                instance.CreatedAt < hourStart ||
                utcNow - instance.CreatedAt.UtcDateTime >= StalePendingThreshold)
            .ToList();
    }

    /// <summary>
    /// A run is stale when it executes in a later UTC hour than the one it was scheduled for; the
    /// current hour's trigger owns that hour, so a late-draining instance must no-op.
    /// </summary>
    public static bool IsStaleRun(DateTime scheduledAtUtc, DateTime currentUtcDateTime) =>
        currentUtcDateTime >= GetUtcHourStart(scheduledAtUtc).AddHours(1).UtcDateTime;
}
