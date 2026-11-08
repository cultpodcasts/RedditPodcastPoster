using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Makes <see cref="SearchReleaseUtcRules"/> bite on a UTC host such as CI (#1191). It re-runs that class in a child
/// <c>dotnet test</c> of this assembly with TZ set east of UTC (<see cref="EastOfUtcChildTestHost"/>), where reading the
/// release as host-local time throws or moves it. No workflow step and no reflection on <see cref="TimeZoneInfo.Local"/>
/// are needed. The child also runs <see cref="SearchReleaseUtcRules.child_host_is_on_pacific_auckland_time"/>, so a
/// silent fall-back to UTC (no tzdata) fails instead of passing. Skipped on Windows, which ignores TZ.
/// </summary>
public class SearchReleaseEastOfUtcHostGuard
{
    [TzHonouringHostFact(DisplayName =
        "When the search release rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where an implicit host-local release conversion would still pass, so only a host east of UTC proves the release ignores the host's zone.")]
    public async Task search_release_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
        EastOfUtcChildTestHost.EastOfUtcZoneResolves().Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            SearchReleaseGuard.EastOfUtcZone);
        var rules = typeof(SearchReleaseUtcRules);
        var expectedCount = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true));

        // Act
        var run = await EastOfUtcChildTestHost.RunAsync(rules, SearchReleaseGuard.ChildVariable);

        // Assert
        run.Exited.Should().BeTrue("the child test host should finish in time");
        run.ExitCode.Should().Be(0, "every search release rule should pass under TZ={0}:\n{1}",
            SearchReleaseGuard.EastOfUtcZone, run.Output);
        run.Total.Should().NotBeNull("the child should report a test total:\n{0}", run.Output);
        run.Total.Should().Be(expectedCount, "the filter should select every rule, not none");
        run.Skipped.Should().Be(0, "the child should run every rule, including the zone check:\n{0}", run.Output);
    }
}
