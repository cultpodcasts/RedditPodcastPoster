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
    [InlineData(OrchestrationRuntimeStatus.ContinuedAsNew)]
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
