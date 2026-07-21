using Microsoft.DurableTask.Client;
using Indexer.Activities;
using Indexer.Models;

namespace Indexer.Orchestrations;

public enum HourlyOrchestrationHealthIssueKind
{
    PriorHourMissing,
    PriorHourStuck,
    PriorHourFailed,
    CurrentHourPrimaryMissed,
    CurrentHourStuck
}

public readonly record struct HourlyOrchestrationHealthIssue(
    HourlyOrchestrationHealthIssueKind Kind,
    int HourUtc,
    string InstanceId,
    OrchestrationRuntimeStatus? Status,
    string Message);

public static class HourlyOrchestrationHealthChecker
{
    /// <summary>
    /// Hourly orchestrations normally finish within a few minutes; flag longer in-progress runs.
    /// </summary>
    public static TimeSpan CurrentHourStuckThreshold { get; set; } = TimeSpan.FromMinutes(8);

    public static IReadOnlyList<HourlyOrchestrationHealthIssue> FindPriorHourIssues(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances)
    {
        var priorHourStart = HourlyOrchestrationCatchUpEvaluator.GetUtcHourStart(utcNow).AddHours(-1);
        var priorHourUtc = priorHourStart.Hour;
        var priorHourInstances = instances
            .Where(instance =>
                instance.CreatedAt >= priorHourStart
                && instance.CreatedAt < priorHourStart.AddHours(1))
            .ToList();

        if (priorHourInstances.Count == 0)
        {
            return
            [
                new HourlyOrchestrationHealthIssue(
                    HourlyOrchestrationHealthIssueKind.PriorHourMissing,
                    priorHourUtc,
                    string.Empty,
                    null,
                    $"No HourlyOrchestration instance was recorded for UTC hour {priorHourUtc}.")
            ];
        }

        var issues = new List<HourlyOrchestrationHealthIssue>();

        foreach (var instance in priorHourInstances.Where(instance =>
                     HourlyOrchestrationCatchUpEvaluator.IsInProgressStatus(instance.Status)
                     || instance.Status == OrchestrationRuntimeStatus.Pending))
        {
            issues.Add(new HourlyOrchestrationHealthIssue(
                HourlyOrchestrationHealthIssueKind.PriorHourStuck,
                priorHourUtc,
                instance.InstanceId,
                instance.Status,
                $"HourlyOrchestration for UTC hour {priorHourUtc} has not completed (status='{instance.Status}', created-at='{instance.CreatedAt.UtcDateTime:O}')."));
        }

        foreach (var instance in priorHourInstances.Where(instance =>
                     instance.Status == OrchestrationRuntimeStatus.Failed))
        {
            issues.Add(new HourlyOrchestrationHealthIssue(
                HourlyOrchestrationHealthIssueKind.PriorHourFailed,
                priorHourUtc,
                instance.InstanceId,
                instance.Status,
                $"HourlyOrchestration for UTC hour {priorHourUtc} failed (instance-id='{instance.InstanceId}')."));
        }

        return issues;
    }

    public static HourlyOrchestrationHealthIssue CreateCurrentHourPrimaryMissedIssue(int hourUtc) =>
        new(
            HourlyOrchestrationHealthIssueKind.CurrentHourPrimaryMissed,
            hourUtc,
            string.Empty,
            null,
            $"HourlyOrchestration for UTC hour {hourUtc} was not started by the primary RunHourly trigger; catch-up is scheduling it.");

    public static IReadOnlyList<HourlyOrchestrationHealthIssue> FindCurrentHourStuckIssues(
        DateTime utcNow,
        IEnumerable<HourlyOrchestrationInstance> instances)
    {
        var hourUtc = utcNow.Hour;
        var elapsedIntoHour = utcNow - HourlyOrchestrationCatchUpEvaluator.GetUtcHourStart(utcNow).UtcDateTime;
        if (elapsedIntoHour < CurrentHourStuckThreshold)
        {
            return [];
        }

        return instances
            .Where(instance => HourlyOrchestrationCatchUpEvaluator.IsInUtcHour(instance.CreatedAt, utcNow))
            .Where(instance =>
                HourlyOrchestrationCatchUpEvaluator.IsInProgressStatus(instance.Status)
                || instance.Status == OrchestrationRuntimeStatus.Pending)
            .Select(instance => new HourlyOrchestrationHealthIssue(
                HourlyOrchestrationHealthIssueKind.CurrentHourStuck,
                hourUtc,
                instance.InstanceId,
                instance.Status,
                $"HourlyOrchestration for UTC hour {hourUtc} is still '{instance.Status}' after {elapsedIntoHour.TotalMinutes:F0} minutes (instance-id='{instance.InstanceId}')."))
            .ToList();
    }
}
