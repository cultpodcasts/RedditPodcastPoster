using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

/// <summary>
/// Business rules for Dynamic Discovery lookback: no SearchSince static floor when a prior
/// Cosmos success exists, and production overlap of 10 minutes (since = lastSuccess - 10m).
/// </summary>
public class DiscoveryLookbackBusinessRulesTests
{
    private static readonly DateTime UtcNow = DateTime.Parse("2026-07-18T14:33:00Z").ToUniversalTime();
    private static readonly TimeSpan SearchSince = TimeSpan.Parse("6:10:00");
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);
    private static readonly DateTime StaticFloor = UtcNow.Subtract(SearchSince);

    // --- Happy / core rules ---

    [Fact(DisplayName =
        "Business rule: Dynamic + recent prior success → since = lastSuccess - 10m overlap; " +
        "MUST NOT re-search back to SearchSince / static floor.")]
    public void Dynamic_recent_prior_success_uses_lastSuccess_minus_overlap_not_static_floor()
    {
        var lastSuccess = UtcNow.AddMinutes(-10);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().BeAfter(StaticFloor);
        since.Should().NotBe(StaticFloor);
    }

    [Fact(DisplayName =
        "Business rule: Dynamic + old prior success (gap recovery) → since = lastSuccess - 10m; " +
        "lookback extends beyond SearchSince to recover the missed window.")]
    public void Dynamic_old_prior_success_recovers_gap_beyond_static_window()
    {
        var lastSuccess = UtcNow.AddHours(-18);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().BeBefore(StaticFloor);
    }

    [Fact(DisplayName =
        "Business rule: Dynamic + no prior Cosmos success → fallback to static UtcNow - SearchSince.")]
    public void Dynamic_no_prior_success_falls_back_to_static_SearchSince()
    {
        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, latestSuccessfulDiscoveryBegan: null);

        since.Should().Be(StaticFloor);
    }

    [Fact(DisplayName =
        "Business rule: Static mode → UtcNow - SearchSince (intentional schedule overlap unchanged), " +
        "even when a lastSuccess watermark exists.")]
    public void Static_mode_always_uses_SearchSince_ignoring_lastSuccess()
    {
        var lastSuccess = UtcNow.AddMinutes(-10);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Static, lastSuccess);

        since.Should().Be(StaticFloor);
        since.Should().NotBe(lastSuccess);
    }

    [Fact(DisplayName =
        "Business rule: DynamicLookbackOverlap default is 10 minutes; " +
        "since = lastSuccess - 10m (still no SearchSince static floor).")]
    public void Dynamic_default_overlap_is_ten_minutes()
    {
        var lastSuccess = UtcNow.AddHours(-2);

        var sinceDefault = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess);
        var sinceExplicit10m = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        DiscoveryLookbackCalculator.DefaultDynamicOverlap.Should().Be(Overlap10m);
        sinceDefault.Should().Be(lastSuccess.Subtract(Overlap10m));
        sinceExplicit10m.Should().Be(lastSuccess.Subtract(Overlap10m));
        sinceDefault.Should().BeAfter(StaticFloor);
    }

    [Fact(DisplayName =
        "Business rule: DynamicLookbackOverlap = 00:00:00 means no overlap; since = lastSuccess " +
        "(still no SearchSince static floor).")]
    public void Dynamic_overlap_zero_means_since_equals_lastSuccess()
    {
        var lastSuccess = UtcNow.AddHours(-2);

        var sinceExplicitZero = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, TimeSpan.Zero);

        sinceExplicitZero.Should().Be(lastSuccess);
        sinceExplicitZero.Should().BeAfter(StaticFloor);
    }

    // --- Failure / recovery scenarios ---

    [Fact(DisplayName =
        "Failure scenario: missed one scheduled 6h slot → next Dynamic run since = lastSuccess - 10m " +
        "(covers the gap; extends past static floor).")]
    public void Failure_missed_one_slot_recovers_from_lastSuccess()
    {
        // Schedule ~08:33 succeeded; 14:33 missed; catch-up at ~20:33
        var catchUpNow = DateTime.Parse("2026-07-18T20:33:00Z").ToUniversalTime();
        var lastSuccess = DateTime.Parse("2026-07-18T08:33:00Z").ToUniversalTime();
        var staticFloor = catchUpNow.Subtract(SearchSince);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            catchUpNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().BeBefore(staticFloor);
        (catchUpNow - since).Should().Be(TimeSpan.FromHours(12) + Overlap10m);
    }

    [Fact(DisplayName =
        "Failure scenario: missed multiple consecutive slots (~24h outage) → since = lastSuccess - 10m, " +
        "recovers the full gap (not capped at SearchSince).")]
    public void Failure_missed_multiple_slots_recovers_full_gap()
    {
        // Last success ~24h ago (four 6h slots missed)
        var lastSuccess = UtcNow.AddHours(-24);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().BeBefore(StaticFloor);
        (UtcNow - since).Should().Be(TimeSpan.FromHours(24) + Overlap10m);
    }

    [Fact(DisplayName =
        "Failure scenario: catch-up run then scheduled run minutes later (recycle / duplicate flood) → " +
        "second run since = first run discoveryBegan - 10m; MUST NOT re-search full SearchSince window.")]
    public void Failure_catchup_then_scheduled_minutes_later_does_not_duplicate_SearchSince_window()
    {
        // Catch-up completed at 14:20; scheduled/recycle fires at 14:33 with lastSuccess = catch-up began
        var catchUpDiscoveryBegan = DateTime.Parse("2026-07-18T14:20:00Z").ToUniversalTime();
        var scheduledRunNow = UtcNow; // 14:33

        var since = DiscoveryLookbackCalculator.ResolveSince(
            scheduledRunNow, SearchSince, DiscoveryLookbackMode.Dynamic, catchUpDiscoveryBegan, Overlap10m);

        since.Should().Be(catchUpDiscoveryBegan.Subtract(Overlap10m));
        since.Should().BeAfter(scheduledRunNow.Subtract(SearchSince));
        (scheduledRunNow - since).Should().Be(TimeSpan.FromMinutes(23)); // 13m gap + 10m overlap
        (scheduledRunNow - since).Should().BeLessThan(SearchSince);
    }

    [Fact(DisplayName =
        "Failure scenario: empty Cosmos history (null lastSuccess) → Dynamic falls back to SearchSince static window.")]
    public void Failure_empty_cosmos_history_falls_back_to_static()
    {
        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, latestSuccessfulDiscoveryBegan: null, Overlap10m);

        since.Should().Be(StaticFloor);
    }

    [Fact(DisplayName =
        "Edge: lastSuccess equal to utcNow (zero-length base window) → since = lastSuccess - 10m; " +
        "does not expand to SearchSince.")]
    public void Edge_lastSuccess_equals_now_yields_overlap_window_not_static_floor()
    {
        var lastSuccess = UtcNow;

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().NotBe(StaticFloor);
        (UtcNow - since).Should().Be(Overlap10m);
    }

    [Fact(DisplayName =
        "Edge: lastSuccess in the future (clock skew) → since follows lastSuccess - overlap without clamping; " +
        "no SearchSince floor applied.")]
    public void Edge_future_lastSuccess_clock_skew_uses_lastSuccess_minus_overlap_without_static_floor()
    {
        var lastSuccess = UtcNow.AddMinutes(5);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccess, Overlap10m);

        since.Should().Be(lastSuccess.Subtract(Overlap10m));
        since.Should().BeBefore(UtcNow);
        since.Should().NotBe(StaticFloor);
    }

    [Fact(DisplayName =
        "Edge: non-UTC lastSuccess Kind is normalized via ToUniversalTime before anchoring.")]
    public void Edge_non_utc_lastSuccess_is_converted_to_universal_time()
    {
        var lastSuccessLocal = DateTime.SpecifyKind(
            DateTime.Parse("2026-07-18T08:33:00"),
            DateTimeKind.Local);

        var since = DiscoveryLookbackCalculator.ResolveSince(
            UtcNow, SearchSince, DiscoveryLookbackMode.Dynamic, lastSuccessLocal, Overlap10m);

        since.Should().Be(lastSuccessLocal.ToUniversalTime().Subtract(Overlap10m));
        since.Kind.Should().Be(DateTimeKind.Utc);
    }
}
