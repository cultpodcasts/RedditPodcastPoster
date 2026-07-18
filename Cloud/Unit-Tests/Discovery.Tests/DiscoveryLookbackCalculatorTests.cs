using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

/// <summary>
/// Focused unit checks for <see cref="DiscoveryLookbackCalculator"/> (see also
/// <see cref="DiscoveryLookbackBusinessRulesTests"/> for named business / failure scenarios).
/// </summary>
public class DiscoveryLookbackCalculatorTests
{
    /// <summary>Today at 14:33 UTC — time-of-day fixed; calendar date is always relative to now.</summary>
    private static readonly DateTime UtcNow =
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(14).AddMinutes(33), DateTimeKind.Utc);

    private static readonly TimeSpan SearchSince = TimeSpan.Parse("6:10:00");
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

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
    public void Dynamic_with_prior_run_uses_last_success_minus_overlap_without_static_floor()
    {
        // Prior success 2h before UtcNow — lastSuccess - 10m is still after SearchSince floor
        var lastRun = UtcNow.AddHours(-2);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, Overlap10m);

        since.Should().Be(lastRun.Subtract(Overlap10m));
        since.Should().BeAfter(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_after_missed_run_extends_lookback_to_last_successful_minus_overlap()
    {
        // Prior success 12h before UtcNow — lastSuccess - 10m is before SearchSince floor
        var lastRun = UtcNow.AddHours(-12);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun, Overlap10m);

        since.Should().Be(lastRun.Subtract(Overlap10m));
        since.Should().BeBefore(UtcNow.Subtract(SearchSince));
    }

    [Fact]
    public void Dynamic_default_overlap_is_ten_minutes()
    {
        var lastRun = UtcNow.AddHours(-2);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastRun);

        since.Should().Be(lastRun.Subtract(Overlap10m));
        DiscoveryLookbackCalculator.DefaultDynamicOverlap.Should().Be(Overlap10m);
    }
}
