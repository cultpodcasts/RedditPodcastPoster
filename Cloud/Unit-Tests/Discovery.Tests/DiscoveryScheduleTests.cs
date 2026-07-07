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
}
