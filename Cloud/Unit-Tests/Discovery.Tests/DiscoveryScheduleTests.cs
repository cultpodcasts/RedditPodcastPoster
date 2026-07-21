using Discovery.Activities;
using Discovery.Models;
using Discovery.Orchestrations;
using Discovery.Services;
using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryScheduleTests
{
    private static readonly IReadOnlyList<TimeOnly> RunTimes =
        DiscoverySchedule.ParseRunTimes(["08:00", "22:00"]);

    private static readonly TimeZoneInfo Uk = DiscoverySchedule.ResolveUkTimeZone();

    [Theory]
    // 08:00 UK in BST = 07:00 UTC → due within grace
    [InlineData("2026-07-19T07:00:05Z", true, "2026-07-19 08:00 UK")]
    [InlineData("2026-07-19T07:10:00Z", true, "2026-07-19 08:00 UK")]
    [InlineData("2026-07-19T07:16:00Z", false, null)]
    // Midday UK — not a slot
    [InlineData("2026-07-19T12:00:00Z", false, null)]
    // 22:00 UK in BST = 21:00 UTC
    [InlineData("2026-07-19T21:00:00Z", true, "2026-07-19 22:00 UK")]
    [InlineData("2026-07-19T21:14:00Z", true, "2026-07-19 22:00 UK")]
    public void TryMatchDueSlot_matches_uk_schedule_within_grace(
        string utcNowText,
        bool expectMatch,
        string? expectedSlotId)
    {
        var utcNow = DateTime.Parse(utcNowText).ToUniversalTime();

        var match = DiscoverySchedule.TryMatchDueSlot(utcNow, RunTimes, DiscoverySchedule.DefaultGrace, Uk);

        if (!expectMatch)
        {
            match.Should().BeNull();
            return;
        }

        match.Should().NotBeNull();
        match!.Value.SlotId.Should().Be(expectedSlotId);
    }

    [Fact]
    public void GetPriorSlot_from_morning_is_previous_evening()
    {
        var morning = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T07:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;

        var prior = DiscoverySchedule.GetPriorSlot(morning, RunTimes, Uk);

        prior.SlotId.Should().Be("2026-07-18 22:00 UK");
    }

    [Fact]
    public void GetPriorSlot_from_evening_is_same_day_morning()
    {
        var evening = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T21:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;

        var prior = DiscoverySchedule.GetPriorSlot(evening, RunTimes, Uk);

        prior.SlotId.Should().Be("2026-07-19 08:00 UK");
    }

    [Fact]
    public void GetLatestDueSlot_maps_midday_to_morning_slot()
    {
        var slot = DiscoverySchedule.GetLatestDueSlot(
            DateTime.Parse("2026-07-19T12:00:00Z").ToUniversalTime(), RunTimes, Uk);

        slot.SlotId.Should().Be("2026-07-19 08:00 UK");
    }

    [Fact]
    public void IsStaleRun_true_when_later_slot_is_due()
    {
        var morning = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T07:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;

        DiscoverySchedule.IsStaleRun(
                morning.SlotStartUtc,
                DateTime.Parse("2026-07-19T21:05:00Z").ToUniversalTime(),
                RunTimes,
                Uk)
            .Should().BeTrue();
    }

    [Fact]
    public void IsStaleRun_false_while_still_in_same_slot_window()
    {
        var morning = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T07:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;

        DiscoverySchedule.IsStaleRun(
                morning.SlotStartUtc,
                DateTime.Parse("2026-07-19T12:00:00Z").ToUniversalTime(),
                RunTimes,
                Uk)
            .Should().BeFalse();
    }

    [Fact]
    public void ParseRunTimes_rejects_non_half_hour_grid()
    {
        var act = () => DiscoverySchedule.ParseRunTimes(["08:15"]);

        act.Should().Throw<FormatException>().WithMessage("*30-minute*");
    }

    [Fact]
    public void ParseRunTimes_defaults_when_empty()
    {
        DiscoverySchedule.ParseRunTimes([]).Should().Equal(DiscoverySchedule.DefaultRunTimesUk);
    }

    [Fact]
    public void Fall_back_ambiguous_hour_uses_stable_first_offset_slot_id()
    {
        // UK clocks fall back 2026-10-25 02:00 → 01:00. Schedule 01:00 would be ambiguous;
        // ensure CreateSlot via GetLatestDueSlot does not throw for a normal 08:00 slot on that day.
        var slot = DiscoverySchedule.GetLatestDueSlot(
            DateTime.Parse("2026-10-25T10:00:00Z").ToUniversalTime(),
            DiscoverySchedule.ParseRunTimes(["08:00", "22:00"]),
            Uk);

        slot.SlotId.Should().Be("2026-10-25 08:00 UK");
    }
}
