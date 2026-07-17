using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;

namespace Indexer.Tests;

public class HourlyOrchestrationCatchUpEvaluatorTests
{
    private static readonly DateTimeOffset HourStart = new(2026, 6, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldScheduleCatchUp_when_no_instance_exists_for_current_hour()
    {
        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                [])
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldScheduleCatchUp_when_only_prior_hour_instances_exist()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddHours(-1), OrchestrationRuntimeStatus.Completed)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldScheduleCatchUp_when_prior_hour_orchestration_is_still_active()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddHours(-2), OrchestrationRuntimeStatus.Running)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldNotScheduleCatchUp_when_instance_already_completed_this_hour()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddSeconds(14), OrchestrationRuntimeStatus.Completed)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances,
                out var skipReason)
            .Should().BeFalse();

        skipReason.Should().Be(HourlyCatchUpSkipReason.CompletedThisHour);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Running)]
    public void ShouldNotScheduleCatchUp_when_hourly_orchestration_is_in_progress_this_hour(
        OrchestrationRuntimeStatus status)
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddSeconds(30), status)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances,
                out var skipReason)
            .Should().BeFalse();

        skipReason.Should().Be(HourlyCatchUpSkipReason.InProgressThisHour);
    }

    [Fact]
    public void ShouldNotScheduleCatchUp_when_recent_pending_instance_exists_this_hour()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddSeconds(30), OrchestrationRuntimeStatus.Pending)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(2),
                instances,
                out var skipReason)
            .Should().BeFalse();

        skipReason.Should().Be(HourlyCatchUpSkipReason.PendingThisHour);
    }

    [Fact]
    public void ShouldScheduleCatchUp_when_pending_instance_is_stale_ghost_from_recycled_worker()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddSeconds(2), OrchestrationRuntimeStatus.Pending)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(8),
                instances)
            .Should().BeTrue();
    }

    [Fact]
    public void GetStalePendingInstances_returns_pending_instances_from_earlier_hours()
    {
        var stalePriorHour = new HourlyOrchestrationInstance(
            HourStart.AddHours(-2), OrchestrationRuntimeStatus.Pending, "prior-hour");
        var instances = new[]
        {
            stalePriorHour,
            new HourlyOrchestrationInstance(HourStart.AddHours(-1), OrchestrationRuntimeStatus.Completed, "completed"),
            new HourlyOrchestrationInstance(HourStart.AddHours(-1), OrchestrationRuntimeStatus.Running, "running")
        };

        HourlyOrchestrationCatchUpEvaluator.GetStalePendingInstances(
                HourStart.UtcDateTime.AddMinutes(3),
                instances)
            .Should().ContainSingle()
            .Which.Should().Be(stalePriorHour);
    }

    [Fact]
    public void GetStalePendingInstances_returns_current_hour_ghost_pending_older_than_threshold()
    {
        var ghost = new HourlyOrchestrationInstance(
            HourStart.AddSeconds(10), OrchestrationRuntimeStatus.Pending, "ghost");

        HourlyOrchestrationCatchUpEvaluator.GetStalePendingInstances(
                HourStart.UtcDateTime.AddMinutes(8),
                [ghost])
            .Should().ContainSingle()
            .Which.Should().Be(ghost);
    }

    [Fact]
    public void GetStalePendingInstances_keeps_fresh_pending_instance_in_current_hour()
    {
        var fresh = new HourlyOrchestrationInstance(
            HourStart.AddMinutes(3), OrchestrationRuntimeStatus.Pending, "fresh");

        HourlyOrchestrationCatchUpEvaluator.GetStalePendingInstances(
                HourStart.UtcDateTime.AddMinutes(4),
                [fresh])
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-07-17T14:03:05Z", "2026-07-17T15:02:30Z", true)]
    [InlineData("2026-07-17T10:08:04Z", "2026-07-17T15:02:30Z", true)]
    [InlineData("2026-07-17T14:59:00Z", "2026-07-17T15:00:00Z", true)]
    [InlineData("2026-07-17T15:03:05Z", "2026-07-17T15:04:00Z", false)]
    [InlineData("2026-07-17T15:03:05Z", "2026-07-17T15:59:59Z", false)]
    [InlineData("2026-07-17T15:00:00Z", "2026-07-17T15:00:00Z", false)]
    public void IsStaleRun_detects_runs_executing_after_their_scheduled_hour(
        string scheduledAtText,
        string currentUtcText,
        bool expected)
    {
        var scheduledAtUtc = DateTime.Parse(scheduledAtText).ToUniversalTime();
        var currentUtc = DateTime.Parse(currentUtcText).ToUniversalTime();

        HourlyOrchestrationCatchUpEvaluator.IsStaleRun(scheduledAtUtc, currentUtc).Should().Be(expected);
    }

    [Fact]
    public void HasHourlyOrchestrationForUtcHour_matches_instance_created_within_hour_boundary()
    {
        HourlyOrchestrationCatchUpEvaluator.HasHourlyOrchestrationForUtcHour(
                HourStart.UtcDateTime.AddMinutes(5),
                [new HourlyOrchestrationInstance(HourStart.AddHours(1).AddTicks(-1), OrchestrationRuntimeStatus.Completed)])
            .Should().BeTrue();

        HourlyOrchestrationCatchUpEvaluator.HasHourlyOrchestrationForUtcHour(
                HourStart.UtcDateTime.AddMinutes(5),
                [new HourlyOrchestrationInstance(HourStart.AddHours(1), OrchestrationRuntimeStatus.Completed)])
            .Should().BeFalse();
    }
}
