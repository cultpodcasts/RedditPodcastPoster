using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ReleaseTimes;

/// <summary>
/// Makes <see cref="YouTubeReleaseTimeUtcRules"/> bite on a UTC host such as CI (#1194). It re-runs that class in a
/// child <c>dotnet test</c> with TZ set east of UTC, where an implicit host-local <see cref="DateTime"/> to
/// <see cref="DateTimeOffset"/> conversion moves the instant by half a day. The child also runs
/// <see cref="YouTubeReleaseTimeUtcRules.child_host_is_east_of_utc"/>, so a silent fall-back to UTC (no tzdata) fails.
/// </summary>
public class YouTubeReleaseTimeEastOfUtcHostGuard
{
    [TzHonouringHostFact(DisplayName =
        "When the YouTube release-time rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where reading an unspecified-kind release as host-local time would still pass, so only a host east of UTC proves YouTube indexing ignores the host's zone.")]
    public async Task youtube_release_time_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
        EastOfUtcChildTestHost.EastOfUtcZoneResolves().Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            EastOfUtcChildTestHost.EastOfUtcZone);
        var rules = typeof(YouTubeReleaseTimeUtcRules);
        var expectedCount = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true));

        // Act
        var run = await EastOfUtcChildTestHost.RunAsync(rules, YouTubeReleaseTimeGuard.ChildVariable);

        // Assert
        run.Exited.Should().BeTrue("the child test host should finish in time");
        run.ExitCode.Should().Be(0, "every YouTube release-time rule should pass under TZ={0}:\n{1}",
            EastOfUtcChildTestHost.EastOfUtcZone, run.Output);
        run.Total.Should().NotBeNull("the child should report a test total:\n{0}", run.Output);
        run.Total.Should().Be(expectedCount, "the filter should select every rule, not none");
        run.Skipped.Should().Be(0, "the child should run every rule, including the zone check:\n{0}", run.Output);
    }
}
