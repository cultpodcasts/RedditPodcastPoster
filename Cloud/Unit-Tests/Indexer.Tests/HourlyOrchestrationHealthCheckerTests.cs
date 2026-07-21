using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;
using Indexer.Activities;
using Indexer.Orchestrations;
using Indexer.Services;
using Indexer.Models;

namespace Indexer.Tests;

public class HourlyOrchestrationHealthCheckerTests
{
    private static readonly DateTimeOffset HourStart = new(2026, 7, 7, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FindPriorHourIssues_reports_missing_when_no_instance_exists()
    {
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            HourStart.UtcDateTime.AddMinutes(8),
            []);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourMissing);
        issues[0].HourUtc.Should().Be(17);
    }

    [Fact]
    public void FindPriorHourIssues_reports_stuck_when_prior_hour_still_running()
    {
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            HourStart.UtcDateTime.AddMinutes(8),
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Running, "abc123")]);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourStuck);
        issues[0].InstanceId.Should().Be("abc123");
        issues[0].HourUtc.Should().Be(17);
    }

    [Fact]
    public void FindPriorHourIssues_is_clear_when_prior_hour_completed()
    {
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            HourStart.UtcDateTime.AddMinutes(8),
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Completed, "abc123")]);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void FindPriorHourIssues_reports_failed_when_prior_hour_failed()
    {
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            HourStart.UtcDateTime.AddMinutes(8),
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Failed, "abc123")]);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourFailed);
    }

    [Fact]
    public void FindCurrentHourStuckIssues_reports_when_in_progress_past_threshold()
    {
        HourlyOrchestrationHealthChecker.CurrentHourStuckThreshold = TimeSpan.FromMinutes(8);

        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            HourStart.UtcDateTime.AddMinutes(10),
            [new HourlyOrchestrationInstance(HourStart.AddMinutes(3), OrchestrationRuntimeStatus.Running, "def456")]);

        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.CurrentHourStuck);
    }

    [Fact]
    public void FindCurrentHourStuckIssues_is_clear_before_threshold()
    {
        HourlyOrchestrationHealthChecker.CurrentHourStuckThreshold = TimeSpan.FromMinutes(8);

        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            HourStart.UtcDateTime.AddMinutes(5),
            [new HourlyOrchestrationInstance(HourStart.AddMinutes(3), OrchestrationRuntimeStatus.Running, "def456")]);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void CreateCurrentHourPrimaryMissedIssue_describes_catch_up_recovery()
    {
        var issue = HourlyOrchestrationHealthChecker.CreateCurrentHourPrimaryMissedIssue(18);

        issue.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.CurrentHourPrimaryMissed);
        issue.HourUtc.Should().Be(18);
        issue.Message.Should().Contain("catch-up");
    }
}
