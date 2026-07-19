using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryLookbackCalculatorTests
{
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

    [Fact]
    public void ResolveSince_uses_last_success_minus_overlap()
    {
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastRun, Overlap10m);

        since.Should().Be(lastRun.Subtract(Overlap10m));
    }

    [Fact]
    public void ResolveSince_default_overlap_is_ten_minutes()
    {
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastRun);

        since.Should().Be(lastRun.Subtract(Overlap10m));
        DiscoveryLookbackCalculator.DefaultDynamicOverlap.Should().Be(Overlap10m);
    }

    [Fact]
    public void ResolveSince_zero_overlap_equals_last_success()
    {
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        DiscoveryLookbackCalculator.ResolveSince(lastRun, TimeSpan.Zero).Should().Be(lastRun);
    }

    [Fact]
    public void ResolveSince_normalizes_non_utc_kind()
    {
        var lastSuccessLocal = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6), DateTimeKind.Local);

        var since = DiscoveryLookbackCalculator.ResolveSince(lastSuccessLocal, Overlap10m);

        since.Should().Be(lastSuccessLocal.ToUniversalTime().Subtract(Overlap10m));
        since.Kind.Should().Be(DateTimeKind.Utc);
    }
}
