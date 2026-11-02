using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// Makes <see cref="AppleCatalogueLookupReleaseUtcRules"/> bite on a UTC host such as CI (#1219). It re-runs that
/// class in a child <c>dotnet test</c> with TZ set east of UTC, where <see cref="DateTime.ToUniversalTime"/> on a
/// zone-less iTunes release would shift the instant. The child also runs
/// <see cref="AppleCatalogueLookupReleaseUtcRules.child_host_is_east_of_utc"/>, so a silent fall-back to UTC fails.
/// </summary>
public class AppleCatalogueLookupReleaseEastOfUtcHostGuard
{
    [TzHonouringHostFact(DisplayName =
        "When the Apple catalogue lookup release UTC rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where ToUniversalTime on a zone-less release would still pass, so only a host east of UTC proves the lookup ignores the host's zone.")]
    public async Task apple_catalogue_lookup_release_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
        EastOfUtcChildTestHost.EastOfUtcZoneResolves().Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            EastOfUtcChildTestHost.EastOfUtcZone);
        var rules = typeof(AppleCatalogueLookupReleaseUtcRules);
        var expectedCount = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true));

        // Act
        var run = await EastOfUtcChildTestHost.RunAsync(rules, AppleCatalogueLookupReleaseGuard.ChildVariable);

        // Assert
        run.Exited.Should().BeTrue("the child test host should finish in time");
        run.ExitCode.Should().Be(0, "every Apple catalogue release UTC rule should pass under TZ={0}:\n{1}",
            EastOfUtcChildTestHost.EastOfUtcZone, run.Output);
        run.Total.Should().NotBeNull("the child should report a test total:\n{0}", run.Output);
        run.Total.Should().Be(expectedCount, "the filter should select every rule, not none");
        run.Skipped.Should().Be(0, "the child should run every rule, including the zone check:\n{0}", run.Output);
    }
}
