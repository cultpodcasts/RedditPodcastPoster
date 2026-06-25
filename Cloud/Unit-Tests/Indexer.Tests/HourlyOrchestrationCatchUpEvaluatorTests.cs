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
    public void ShouldNotScheduleCatchUp_when_instance_already_started_this_hour()
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddSeconds(14), OrchestrationRuntimeStatus.Completed)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Pending)]
    [InlineData(OrchestrationRuntimeStatus.Running)]
    [InlineData(OrchestrationRuntimeStatus.ContinuedAsNew)]
    public void ShouldNotScheduleCatchUp_when_hourly_orchestration_is_still_active(OrchestrationRuntimeStatus status)
    {
        var instances = new[]
        {
            new HourlyOrchestrationInstance(HourStart.AddHours(-2), status)
        };

        HourlyOrchestrationCatchUpEvaluator.ShouldScheduleCatchUp(
                HourStart.UtcDateTime.AddMinutes(5),
                instances)
            .Should().BeFalse();
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
