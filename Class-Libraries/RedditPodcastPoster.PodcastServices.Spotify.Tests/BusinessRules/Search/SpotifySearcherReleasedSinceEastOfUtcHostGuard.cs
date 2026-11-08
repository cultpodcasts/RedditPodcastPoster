using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Search;

/// <summary>
/// Makes <see cref="SpotifySearcherReleasedSinceUtcRules"/> bite on a UTC host such as CI (#1219). It re-runs that
/// class in a child <c>dotnet test</c> with TZ set east of UTC, where <see cref="DateTime.ToUniversalTime"/> on an
/// unspecified <c>ReleasedSince</c> floors the search window to the previous day. The child also runs
/// <see cref="SpotifySearcherReleasedSinceUtcRules.child_host_is_east_of_utc"/>, so a silent fall-back to UTC fails.
/// </summary>
public class SpotifySearcherReleasedSinceEastOfUtcHostGuard
{
    [TzHonouringHostFact(DisplayName =
        "When the Spotify searcher ReleasedSince UTC rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where ToUniversalTime on an unspecified ReleasedSince would still pass, so only a host east of UTC proves the day floor ignores the host's zone.")]
    public async Task spotify_searcher_released_since_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
        EastOfUtcChildTestHost.EastOfUtcZoneResolves().Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            EastOfUtcChildTestHost.EastOfUtcZone);
        var rules = typeof(SpotifySearcherReleasedSinceUtcRules);
        var expectedCount = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true));

        // Act
        var run = await EastOfUtcChildTestHost.RunAsync(rules, SpotifySearcherReleasedSinceGuard.ChildVariable);

        // Assert
        run.Exited.Should().BeTrue("the child test host should finish in time");
        run.ExitCode.Should().Be(0, "every Spotify ReleasedSince UTC rule should pass under TZ={0}:\n{1}",
            EastOfUtcChildTestHost.EastOfUtcZone, run.Output);
        run.Total.Should().NotBeNull("the child should report a test total:\n{0}", run.Output);
        run.Total.Should().Be(expectedCount, "the filter should select every rule, not none");
        run.Skipped.Should().Be(0, "the child should run every rule, including the zone check:\n{0}", run.Output);
    }
}
