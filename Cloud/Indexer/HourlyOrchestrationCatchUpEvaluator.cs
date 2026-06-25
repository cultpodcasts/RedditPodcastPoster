using Microsoft.DurableTask.Client;

namespace Indexer;

public readonly record struct HourlyOrchestrationInstance(DateTimeOffset CreatedAt, OrchestrationRuntimeStatus Status);

public static class HourlyOrchestrationCatchUpEvaluator
{
    private static readonly OrchestrationRuntimeStatus[] ActiveStatuses =
    [
        OrchestrationRuntimeStatus.Pending,
        OrchestrationRuntimeStatus.Running,
        OrchestrationRuntimeStatus.ContinuedAsNew
    ];

    public static bool HasHourlyOrchestrationForUtcHour(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances)
    {
        var hourStart = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, TimeSpan.Zero);
        var hourEnd = hourStart.AddHours(1);

        return instances.Any(instance =>
            instance.CreatedAt >= hourStart && instance.CreatedAt < hourEnd);
    }

    public static bool HasActiveHourlyOrchestration(IEnumerable<HourlyOrchestrationInstance> instances) =>
        instances.Any(instance => ActiveStatuses.Contains(instance.Status));

    public static bool ShouldScheduleCatchUp(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances) =>
        !HasActiveHourlyOrchestration(instances) && !HasHourlyOrchestrationForUtcHour(utcNow, instances);
}
