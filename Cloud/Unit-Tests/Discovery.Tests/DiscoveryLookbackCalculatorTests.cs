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
    public void Dynamic_with_recent_prior_run_keeps_at_least_static_window()
    {
        // Last run ~6h ago — with 10m overlap, dynamic since ≈ static since
        var lastRun = DateTime.Parse("2026-07-18T08:33:00Z").ToUniversalTime();

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, TimeSpan.FromMinutes(10));

        since.Should().Be(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_after_missed_run_extends_lookback_to_last_successful_minus_overlap()
    {
        // Missed 08:33; last success 02:33 → look back further than static 6h10m
        var lastRun = DateTime.Parse("2026-07-18T02:33:00Z").ToUniversalTime();
        var overlap = TimeSpan.FromMinutes(10);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, overlap);

        since.Should().Be(lastRun.Subtract(overlap));
        since.Should().BeBefore(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_with_very_recent_prior_run_does_not_shorten_below_static()
    {
        var lastRun = UtcNow.AddHours(-1);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, TimeSpan.FromMinutes(10));

        since.Should().Be(UtcNow.Subtract(SearchSince));
    }
}
