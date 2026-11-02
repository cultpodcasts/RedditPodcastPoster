using Discovery.Services;
using FluentAssertions;
using Xunit;

namespace Discovery.Tests;

public class DiscoveryLookbackCalculatorTests
{
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);

    [Fact(DisplayName =
        "When ResolveSince is given a UTC last success and a 10-minute overlap, then since is lastSuccess minus that overlap.")]
    public void ResolveSince_uses_last_success_minus_overlap()
    {
        // Arrange
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(lastRun, Overlap10m);

        // Assert
        since.Should().Be(lastRun.Subtract(Overlap10m));
    }

    [Fact(DisplayName =
        "When ResolveSince is given no overlap, then it uses the default ten-minute Dynamic overlap.")]
    public void ResolveSince_default_overlap_is_ten_minutes()
    {
        // Arrange
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(lastRun);

        // Assert
        since.Should().Be(lastRun.Subtract(Overlap10m));
        DiscoveryLookbackCalculator.DefaultDynamicOverlap.Should().Be(Overlap10m);
    }

    [Fact(DisplayName =
        "When DynamicLookbackOverlap is zero, then ResolveSince returns lastSuccess unchanged.")]
    public void ResolveSince_zero_overlap_equals_last_success()
    {
        // Arrange
        var lastRun = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc);

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(lastRun, TimeSpan.Zero);

        // Assert
        since.Should().Be(lastRun);
    }

    [Fact(DisplayName =
        "When latestSuccessfulDiscoveryBegan is Local kind, then ResolveSince converts it to UTC before subtracting overlap.")]
    public void ResolveSince_normalizes_non_utc_kind()
    {
        // Arrange
        var lastSuccessLocal = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6), DateTimeKind.Local);

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(lastSuccessLocal, Overlap10m);

        // Assert
        since.Should().Be(lastSuccessLocal.ToUniversalTime().Subtract(Overlap10m));
        since.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When latestSuccessfulDiscoveryBegan is unspecified-kind, ResolveSince treats it as UTC (not host-local).")]
    public void ResolveSince_reads_unspecified_as_utc()
    {
        // Arrange
        var wallClock = DateTime.UtcNow.Date.AddHours(12).AddMinutes(30);
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(unspecified, Overlap10m);

        // Assert
        since.Should().Be(DateTime.SpecifyKind(wallClock, DateTimeKind.Utc).Subtract(Overlap10m));
        since.Kind.Should().Be(DateTimeKind.Utc);
    }
}
