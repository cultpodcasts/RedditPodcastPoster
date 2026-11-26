using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Twitter.Clients;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

public class XApiRateLimitPolicyRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When LookUps has no rate-limit state, then X is not skipped, because a missing document means the account is clear to call.")]
    public void missing_state_is_not_skipped()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));

        // Act
        var skip = XApiRateLimitPolicy.GetActiveSkip(null, now);

        // Assert
        skip.Should().BeNull();
    }

    [Fact(DisplayName =
        "When RateLimitResetUtc is still in the future, then X is skipped as rate-limited until that instant, because x-rate-limit-reset must survive Function recycle.")]
    public void future_rate_limit_reset_skips()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState { RateLimitResetUtc = now.AddMinutes(20) };

        // Act
        var skip = XApiRateLimitPolicy.GetActiveSkip(state, now);

        // Assert
        skip.Should().Be(XApiSkipReason.RateLimited);
    }

    [Fact(DisplayName =
        "When RateLimitResetUtc has already passed, then X is not skipped, because the rate-limit window has elapsed.")]
    public void past_rate_limit_reset_does_not_skip()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState { RateLimitResetUtc = now.AddMinutes(-1) };

        // Act
        var skip = XApiRateLimitPolicy.GetActiveSkip(state, now);

        // Assert
        skip.Should().BeNull();
    }

    [Fact(DisplayName =
        "When UsageCappedUntilUtc is still in the future and UpdatedUtc is today, then X is skipped as usage-capped even if a rate-limit reset is also set, because depleted credits are a longer block than a short rate window.")]
    public void usage_cap_takes_precedence_over_rate_limit()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState
        {
            RateLimitResetUtc = now.AddMinutes(5),
            UsageCappedUntilUtc = now.AddDays(2),
            UpdatedUtc = now
        };

        // Act
        var skip = XApiRateLimitPolicy.GetActiveSkip(state, now);

        // Assert
        skip.Should().Be(XApiSkipReason.UsageCapped);
    }

    [Fact(DisplayName =
        "When UsageCappedUntilUtc is still in the future but UpdatedUtc is on a prior UTC day, then X is not skipped, because one probe per day lets a false or short-lived usage-cap clear mid-window on success.")]
    public void usage_cap_allows_one_probe_per_utc_day()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState
        {
            UsageCappedUntilUtc = now.AddDays(20),
            UpdatedUtc = now.AddDays(-1)
        };

        // Act
        var skip = XApiRateLimitPolicy.GetActiveSkip(state, now);

        // Assert
        skip.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a rate-limit response includes x-rate-limit-reset Unix seconds, then the do-not-retry-before instant is that UTC time.")]
    public void apply_rate_limit_uses_reset_header()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var reset = now.AddMinutes(15);
        var state = new XApiRateLimitState();
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["x-rate-limit-reset"] = [new DateTimeOffset(reset).ToUnixTimeSeconds().ToString()]
        };

        // Act
        var parsed = XApiRateLimitPolicy.TryParseResetUtc(headers);
        XApiRateLimitPolicy.ApplyRateLimit(state, parsed, now);

        // Assert
        parsed.Should().Be(reset);
        state.RateLimitResetUtc.Should().Be(reset);
        state.LastReason.Should().Be(XApiRateLimitPolicy.RateLimitReason);
        state.UsageCappedUntilUtc.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a rate-limit response has no usable reset header, then the skip lasts until the next UTC hour, so the following hourly Indexer run can retry.")]
    public void apply_rate_limit_without_header_skips_to_next_hour()
    {
        // Arrange
        var now = DomainTestFixture.UtcToday.Add(new TimeSpan(12, 34, 56));
        var state = new XApiRateLimitState();

        // Act
        XApiRateLimitPolicy.ApplyRateLimit(state, null, now);

        // Assert
        state.RateLimitResetUtc.Should().Be(DomainTestFixture.UtcToday.AddHours(13));
    }

    [Fact(DisplayName =
        "When usage is capped, then the skip lasts until the start of the next UTC calendar month, because X usage credits are typically monthly.")]
    public void apply_usage_cap_skips_to_next_month()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState();

        // Act
        XApiRateLimitPolicy.ApplyUsageCap(state, now);

        // Assert
        var expected = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        state.UsageCappedUntilUtc.Should().Be(expected);
        state.LastReason.Should().Be(XApiRateLimitPolicy.UsageCappedReason);
        state.RateLimitResetUtc.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a successful X call clears the LookUps state, then both rate-limit and usage-cap windows are removed, because a success means the account can be called again.")]
    public void clear_removes_both_windows()
    {
        // Arrange
        var now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
        var state = new XApiRateLimitState
        {
            RateLimitResetUtc = now.AddMinutes(10),
            UsageCappedUntilUtc = now.AddDays(1),
            LastReason = XApiRateLimitPolicy.RateLimitReason
        };

        // Act
        var had = XApiRateLimitPolicy.Clear(state, now);

        // Assert
        had.Should().BeTrue();
        state.RateLimitResetUtc.Should().BeNull();
        state.UsageCappedUntilUtc.Should().BeNull();
        state.LastReason.Should().BeNull();
    }

    [Fact(DisplayName =
        "When x-rate-limit-reset is malformed, then TryParseResetUtc returns null rather than throwing, so the client can still persist a short default skip.")]
    public void malformed_reset_header_parses_as_null()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["x-rate-limit-reset"] = [_fixture.Create<string>()]
        };

        // Act
        var parsed = XApiRateLimitPolicy.TryParseResetUtc(headers);

        // Assert
        parsed.Should().BeNull();
    }
}
