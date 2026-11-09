using FluentAssertions;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Serialization;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

/// <summary>
/// Discover console <c>-t</c>/<c>--since</c> treats unspecified-kind as UTC (#1212).
/// <see cref="DateTime.ToUniversalTime"/> would shift the baseline on a host east of UTC.
/// </summary>
public class DiscoverCliSinceUtcRules
{
    private readonly DomainTestFixture _fixture = new();

    private DateTime CreateUnspecifiedPastInstant() =>
        DateTime.SpecifyKind(
            DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 2), _fixture.CreateNonMidnightTimeOfDay()),
            DateTimeKind.Unspecified);

    [Fact(DisplayName =
        "When Discover CLI since is unspecified-kind, then Resolve returns that same UTC instant with Kind Utc, because an unspecified CLI baseline is UTC and ToUniversalTime on a host east of UTC would shift the discovery window.")]
    public void cli_since_reads_unspecified_as_utc()
    {
        // Arrange
        var since = CreateUnspecifiedPastInstant();
        var expected = UtcDateTimes.ToUtc(since);

        // Act
        var resolved = DiscoverCliSince.Resolve(since);

        // Assert
        resolved.Should().Be(expected);
        resolved.Kind.Should().Be(DateTimeKind.Utc);
        resolved.Should().Be(DateTime.SpecifyKind(since, DateTimeKind.Utc));
    }

    [Fact(DisplayName =
        "When Discover CLI since is already UTC and in the past, then Resolve returns that same instant.")]
    public void cli_since_keeps_utc_kind()
    {
        // Arrange
        var since = DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 100 + 1), _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var resolved = DiscoverCliSince.Resolve(since);

        // Assert
        resolved.Should().Be(since);
        resolved.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When Discover CLI since is after utcNow, then Resolve throws, because a future baseline is invalid.")]
    public void cli_since_rejects_future()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var since = now.AddHours(1);

        // Act
        var act = () => DiscoverCliSince.Resolve(since, now);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*future*");
    }
}
