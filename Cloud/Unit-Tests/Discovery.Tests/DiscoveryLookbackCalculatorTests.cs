using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryLookbackCalculatorTests
{
    private static readonly DateTime UtcNow = DateTime.Parse("2026-07-18T14:33:00Z").ToUniversalTime();
    private static readonly TimeSpan SearchSince = TimeSpan.Parse("6:10:00");

    [Fact]
    public void Static_mode_uses_SearchSince_from_utc_now()
    {
        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Static, latestSuccessfulDiscoveryBegan: null);

        since.Should().Be(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_without_prior_run_falls_back_to_static()
    {
        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, latestSuccessfulDiscoveryBegan: null);

        since.Should().Be(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_with_prior_run_uses_last_success_without_static_floor()
    {
        // Last run ~6h ago — since is lastSuccess, not SearchSince floor
        var lastRun = DateTime.Parse("2026-07-18T08:33:00Z").ToUniversalTime();

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, TimeSpan.Zero);

        since.Should().Be(lastRun);
        since.Should().BeAfter(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_after_missed_run_extends_lookback_to_last_successful()
    {
        // Missed 08:33; last success 02:33 → look back further than static 6h10m
        var lastRun = DateTime.Parse("2026-07-18T02:33:00Z").ToUniversalTime();

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, TimeSpan.Zero);

        since.Should().Be(lastRun);
        since.Should().BeBefore(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_with_very_recent_prior_run_uses_last_success_not_static_window()
    {
        // Today's bug: last success ~minutes ago must not re-search the full SearchSince window
        var lastRun = UtcNow.AddHours(-1);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, TimeSpan.Zero);

        since.Should().Be(lastRun);
        since.Should().NotBe(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_with_overlap_subtracts_from_last_success_only()
    {
        var lastRun = UtcNow.AddHours(-1);
        var overlap = TimeSpan.FromMinutes(10);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, overlap);

        since.Should().Be(lastRun.Subtract(overlap));
        since.Should().BeAfter(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_default_overlap_is_zero()
    {
        var lastRun = UtcNow.AddHours(-2);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun);

        since.Should().Be(lastRun);
        DiscoveryLookbackCalculator.DefaultDynamicOverlap.Should().Be(TimeSpan.Zero);
    }
}
