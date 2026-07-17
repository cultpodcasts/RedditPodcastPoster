using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryOrchestrationHealthCheckerTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 7, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void FindFailedInstances_reports_failed_orchestrations()
    {
        var issues = DiscoveryOrchestrationHealthChecker.FindFailedInstances(
        [
            new DiscoveryOrchestrationInstance(CreatedAt, OrchestrationRuntimeStatus.Failed, "abc123")
        ]);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(DiscoveryOrchestrationHealthIssueKind.Failed);
    }

    [Fact]
    public void FindIncompleteInstances_reports_stuck_in_progress_runs()
    {
        DiscoveryOrchestrationHealthChecker.CompletionThreshold = TimeSpan.FromMinutes(10);

        var issues = DiscoveryOrchestrationHealthChecker.FindIncompleteInstances(
            CreatedAt.UtcDateTime.AddMinutes(15),
            [new DiscoveryOrchestrationInstance(CreatedAt, OrchestrationRuntimeStatus.Running, "abc123")]);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(DiscoveryOrchestrationHealthIssueKind.StuckInProgress);
    }

    [Fact]
    public void FindIncompleteInstances_is_clear_for_recent_in_progress_runs()
    {
        DiscoveryOrchestrationHealthChecker.CompletionThreshold = TimeSpan.FromMinutes(10);

        var issues = DiscoveryOrchestrationHealthChecker.FindIncompleteInstances(
            CreatedAt.UtcDateTime.AddMinutes(5),
            [new DiscoveryOrchestrationInstance(CreatedAt, OrchestrationRuntimeStatus.Running, "abc123")]);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void GetStalePendingInstances_returns_pending_instances_from_earlier_slots()
    {
        var currentSlotStart = new DateTimeOffset(2026, 7, 7, 14, 33, 0, TimeSpan.Zero);
        var stale = new DiscoveryOrchestrationInstance(
            currentSlotStart.AddHours(-6).AddSeconds(5), OrchestrationRuntimeStatus.Pending, "stale");
        var instances = new[]
        {
            stale,
            new DiscoveryOrchestrationInstance(
                currentSlotStart.AddHours(-6).AddSeconds(5), OrchestrationRuntimeStatus.Completed, "completed"),
            new DiscoveryOrchestrationInstance(
                currentSlotStart.AddSeconds(5), OrchestrationRuntimeStatus.Pending, "current-slot")
        };

        DiscoveryOrchestrationHealthChecker.GetStalePendingInstances(currentSlotStart, instances)
            .Should().ContainSingle()
            .Which.Should().Be(stale);
    }

    [Fact]
    public void CreateBlockedByActiveRunIssue_describes_skipped_trigger()
    {
        var issue = DiscoveryOrchestrationHealthChecker.CreateBlockedByActiveRunIssue(
            new DiscoveryOrchestrationInstance(CreatedAt, OrchestrationRuntimeStatus.Running, "abc123"));

        issue.Kind.Should().Be(DiscoveryOrchestrationHealthIssueKind.BlockedByActiveRun);
        issue.Message.Should().Contain("skipped scheduling");
    }
}
