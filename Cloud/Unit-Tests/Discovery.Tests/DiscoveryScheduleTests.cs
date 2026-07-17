using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryScheduleTests
{
    [Theory]
    [InlineData("2026-07-07T14:30:10Z", "2026-07-07T08:33:00Z")]
    [InlineData("2026-07-07T14:33:05Z", "2026-07-07T14:33:00Z")]
    [InlineData("2026-07-07T02:40:00Z", "2026-07-07T02:33:00Z")]
    public void GetSlotStartForTime_maps_instance_time_to_scheduled_slot(string utcNowText, string expectedSlotText)
    {
        var utcNow = DateTime.Parse(utcNowText).ToUniversalTime();
        var expected = DateTimeOffset.Parse(expectedSlotText);

        DiscoverySchedule.GetSlotStartForTime(utcNow).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-07-07T08:33:05Z", "2026-07-07T14:35:00Z", true)]
    [InlineData("2026-07-07T02:33:05Z", "2026-07-07T14:35:00Z", true)]
    [InlineData("2026-07-07T14:33:05Z", "2026-07-07T14:35:00Z", false)]
    [InlineData("2026-07-07T14:33:05Z", "2026-07-07T20:32:59Z", false)]
    [InlineData("2026-07-07T14:33:05Z", "2026-07-07T20:33:00Z", true)]
    public void IsStaleRun_detects_runs_executing_after_their_scheduled_slot(
        string scheduledAtText,
        string currentUtcText,
        bool expected)
    {
        var scheduledAtUtc = DateTime.Parse(scheduledAtText).ToUniversalTime();
        var currentUtc = DateTime.Parse(currentUtcText).ToUniversalTime();

        DiscoverySchedule.IsStaleRun(scheduledAtUtc, currentUtc).Should().Be(expected);
    }
}
