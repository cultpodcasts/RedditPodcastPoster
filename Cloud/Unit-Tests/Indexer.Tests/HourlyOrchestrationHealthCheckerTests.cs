using Microsoft.DurableTask.Client;
using AutoFixture;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class HourlyOrchestrationHealthCheckerTests
{
    private static readonly DateTimeOffset HourStart =
        new(DomainTestFixture.UtcToday.AddHours(18), TimeSpan.Zero);

    private readonly Fixture _fixture = new();

    public HourlyOrchestrationHealthCheckerTests()
    {
        HourlyOrchestrationHealthChecker.CurrentHourStuckThreshold = TimeSpan.FromMinutes(8);
    }

    [Fact(DisplayName =
        "When no HourlyOrchestration instance was recorded for the prior hour, the prior hour is reported missing.")]
    public void FindPriorHourIssues_reports_missing_when_no_instance_exists()
    {
        // Arrange
        var utcNow = HourStart.UtcDateTime.AddMinutes(8);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(utcNow, []);

        // Assert
        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourMissing);
        issues[0].HourUtc.Should().Be(17);
    }

    [Fact(DisplayName =
        "When the prior hour's run is still running, the prior hour is reported stuck with that run's instance id.")]
    public void FindPriorHourIssues_reports_stuck_when_prior_hour_still_running()
    {
        // Arrange
        var utcNow = HourStart.UtcDateTime.AddMinutes(8);
        var instanceId = _fixture.Create<string>();
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            utcNow,
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Running, instanceId)]);

        // Assert
        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourStuck);
        issues[0].InstanceId.Should().Be(instanceId);
        issues[0].HourUtc.Should().Be(17);
    }

    [Fact(DisplayName =
        "When the prior hour's run completed, the prior hour has no issue.")]
    public void FindPriorHourIssues_is_clear_when_prior_hour_completed()
    {
        // Arrange
        var utcNow = HourStart.UtcDateTime.AddMinutes(8);
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            utcNow,
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Completed, _fixture.Create<string>())]);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the prior hour's run failed, the prior hour is reported failed.")]
    public void FindPriorHourIssues_reports_failed_when_prior_hour_failed()
    {
        // Arrange
        var utcNow = HourStart.UtcDateTime.AddMinutes(8);
        var priorHour = HourStart.AddHours(-1).AddMinutes(3);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindPriorHourIssues(
            utcNow,
            [new HourlyOrchestrationInstance(priorHour, OrchestrationRuntimeStatus.Failed, _fixture.Create<string>())]);

        // Assert
        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.PriorHourFailed);
    }

    [Fact(DisplayName =
        "A run that started 8 minutes ago and is still running is reported as stuck, because the alarm clock is the run's start.")]
    public void FindCurrentHourStuckIssues_reports_when_in_progress_past_threshold()
    {
        // Arrange
        var started = HourStart.AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(11);
        var instanceId = _fixture.Create<string>();

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(started, OrchestrationRuntimeStatus.Running, instanceId)]);

        // Assert
        var issue = issues.Should().ContainSingle().Subject;
        issue.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.CurrentHourStuck);
        issue.InstanceId.Should().Be(instanceId);
        issue.HourUtc.Should().Be(HourStart.Hour);
    }

    [Fact(DisplayName =
        "A run that started 5 minutes ago is not reported as stuck, even 8 minutes into the hour, because " +
        "the threshold counts from when the run started, not from the top of the hour.")]
    public void FindCurrentHourStuckIssues_is_clear_for_run_started_five_minutes_ago_at_eight_minutes_into_hour()
    {
        // Arrange
        var started = HourStart.AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(8).AddSeconds(4);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(started, OrchestrationRuntimeStatus.Running, _fixture.Create<string>())]);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A pending run that was created 8 minutes ago is reported as stuck, because it never started working.")]
    public void FindCurrentHourStuckIssues_reports_pending_run_past_threshold()
    {
        // Arrange
        var created = HourStart.AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(11);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(created, OrchestrationRuntimeStatus.Pending, _fixture.Create<string>())]);

        // Assert
        issues.Should().ContainSingle()
            .Which.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.CurrentHourStuck);
    }

    [Theory(DisplayName =
        "A run that already finished (completed, failed or terminated) is never reported as stuck, however long ago it started.")]
    [InlineData(OrchestrationRuntimeStatus.Completed)]
    [InlineData(OrchestrationRuntimeStatus.Failed)]
    [InlineData(OrchestrationRuntimeStatus.Terminated)]
    public void FindCurrentHourStuckIssues_is_clear_for_finished_run(OrchestrationRuntimeStatus status)
    {
        // Arrange
        var started = HourStart.AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(40);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(started, status, _fixture.Create<string>())]);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A run that started in the prior hour is not a current-hour stuck issue, because the prior-hour checks own it.")]
    public void FindCurrentHourStuckIssues_ignores_run_started_in_prior_hour()
    {
        // Arrange
        var started = HourStart.AddHours(-1).AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(8);

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(started, OrchestrationRuntimeStatus.Running, _fixture.Create<string>())]);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The stuck message names the minutes since the run started and its instance id.")]
    public void FindCurrentHourStuckIssues_message_names_minutes_since_start_and_instance_id()
    {
        // Arrange
        var started = HourStart.AddMinutes(3);
        var utcNow = HourStart.UtcDateTime.AddMinutes(13);
        var instanceId = _fixture.Create<string>();

        // Act
        var issues = HourlyOrchestrationHealthChecker.FindCurrentHourStuckIssues(
            utcNow,
            [new HourlyOrchestrationInstance(started, OrchestrationRuntimeStatus.Running, instanceId)]);

        // Assert
        var message = issues.Should().ContainSingle().Subject.Message;
        message.Should().Contain("10 minutes after it started");
        message.Should().Contain($"instance-id='{instanceId}'");
    }

    [Fact(DisplayName =
        "The primary-missed issue says that catch-up is scheduling the run.")]
    public void CreateCurrentHourPrimaryMissedIssue_describes_catch_up_recovery()
    {
        // Arrange
        var hourUtc = HourStart.Hour;

        // Act
        var issue = HourlyOrchestrationHealthChecker.CreateCurrentHourPrimaryMissedIssue(hourUtc);

        // Assert
        issue.Kind.Should().Be(HourlyOrchestrationHealthIssueKind.CurrentHourPrimaryMissed);
        issue.HourUtc.Should().Be(hourUtc);
        issue.Message.Should().Contain("catch-up");
    }
}
