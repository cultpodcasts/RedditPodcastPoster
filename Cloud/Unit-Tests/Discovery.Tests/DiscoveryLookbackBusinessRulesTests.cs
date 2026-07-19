using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

/// <summary>
/// Business rules for Dynamic-only Discovery lookback (fail-closed when no watermark is a
/// resolver concern — see <see cref="DiscoveryLookbackResolverBusinessRulesTests"/>).
/// </summary>
public class DiscoveryLookbackBusinessRulesTests
{
    private static readonly DateTime UtcNow =
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(14).AddMinutes(33), DateTimeKind.Utc);

    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

    [Fact(DisplayName =
        "Business rule: Dynamic + recent prior success → since = lastSuccess - 10m overlap.")]
    public void Dynamic_recent_prior_success_uses_lastSuccess_minus_overlap()
    {
        var lastSuccess = UtcNow.AddMinutes(-10);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
    }

    [Fact(DisplayName =
        "Business rule: Dynamic + old prior success (gap recovery) → since = lastSuccess - 10m.")]
    public void Dynamic_old_prior_success_recovers_gap()
    {
        var lastSuccess = UtcNow.AddHours(-18);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        (UtcNow - since).Should().Be(TimeSpan.FromHours(18) + Overlap10m);
    }

    [Fact(DisplayName =
        "Business rule: DynamicLookbackOverlap = 00:00:00 means no overlap; since = lastSuccess.")]
    public void Dynamic_overlap_zero_means_since_equals_lastSuccess()
    {
        var lastSuccess = UtcNow.AddHours(-2);

        DiscoveryLookbackCalculator.ResolveSince(lastSuccess, TimeSpan.Zero).Should().Be(lastSuccess);
    }

    [Fact(DisplayName =
        "Failure scenario: missed one scheduled slot → next Dynamic run since = lastSuccess - 10m.")]
    public void Failure_missed_one_slot_recovers_from_lastSuccess()
    {
        var catchUpNow = UtcNow.AddHours(14); // evening after morning miss
        var lastSuccess = UtcNow.AddHours(-10);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        (catchUpNow - since).Should().BeGreaterThan(TimeSpan.FromHours(14));
    }

    [Fact(DisplayName =
        "Failure scenario: catch-up then scheduled minutes later → second run since = first - 10m.")]
    public void Failure_catchup_then_scheduled_minutes_later_uses_overlap_only()
    {
        var catchUpDiscoveryBegan = UtcNow.AddMinutes(-13);

        var since = DiscoveryLookbackCalculator.ResolveSince(catchUpDiscoveryBegan, Overlap10m);

        since.Should().Be(catchUpDiscoveryBegan.Subtract(Overlap10m));
        (UtcNow - since).Should().Be(TimeSpan.FromMinutes(23));
    }
}
