using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace FunctionHost.Tests.Api.Extensions;

/// <summary>Shared settings for <see cref="UtcJsonDtoContractEastOfUtcHostGuard"/> and the child run it starts.</summary>
internal static class UtcJsonDtoContractGuard
{
    /// <summary>Set to <c>1</c> by the guard in the child test host only.</summary>
    public const string ChildVariable = "UTC_JSON_DTO_CONTRACT_GUARD";

    public static bool InChild => Environment.GetEnvironmentVariable(ChildVariable) == "1";
}

/// <summary>
/// A fact that runs only inside the guard's child test host, where it proves the child really is on
/// <see cref="EastOfUtcChildTestHost.EastOfUtcZone"/> time. Elsewhere it is skipped, because the host zone is not set.
/// </summary>
public sealed class UtcJsonDtoContractGuardChildFactAttribute : FactAttribute
{
    public UtcJsonDtoContractGuardChildFactAttribute()
    {
        if (!UtcJsonDtoContractGuard.InChild)
        {
            Skip = $"Runs only in the {nameof(UtcJsonDtoContractEastOfUtcHostGuard)} child test host " +
                   $"({UtcJsonDtoContractGuard.ChildVariable}=1, TZ={EastOfUtcChildTestHost.EastOfUtcZone}).";
        }
    }
}

/// <summary>
/// A fact that is skipped on Windows, where the TZ variable is ignored, so a child test host cannot be moved east of
/// UTC and the guard would prove nothing.
/// </summary>
public sealed class UtcJsonDtoContractTzHonouringHostFactAttribute : FactAttribute
{
    public UtcJsonDtoContractTzHonouringHostFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Windows ignores TZ, so the child test host cannot be put on Pacific/Auckland time; " +
                   "the guard runs on Linux and macOS (CI is Linux).";
        }
    }
}

/// <summary>
/// Makes <see cref="UtcJsonDtoContractRules"/> bite on a UTC host such as CI (#1212). On UTC, a zone-less or
/// Local-kind <see cref="DateTime"/> has the same ticks as UTC, so a converter that read it as host-local time would
/// still write the expected <c>Z</c>. The guard re-runs that class in a child <c>dotnet test</c> with TZ set east of
/// UTC, where such a read moves the instant by half a day.
/// </summary>
public class UtcJsonDtoContractEastOfUtcHostGuard
{
    [UtcJsonDtoContractTzHonouringHostFact(DisplayName =
        "When the Api DTO UTC Z contract is re-run in a child test host on Pacific/Auckland time, then every case runs and passes, because on a UTC host a converter that read zone-less or local date-times as host-local time would still write the expected Z.")]
    public async Task utc_json_dto_contract_passes_on_a_host_east_of_utc()
    {
        // Arrange
        EastOfUtcChildTestHost.EastOfUtcZoneResolves().Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            EastOfUtcChildTestHost.EastOfUtcZone);
        var rules = typeof(UtcJsonDtoContractRules);
        var facts = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true) &&
                                                  !m.IsDefined(typeof(TheoryAttribute), true));
        var expectedCount = facts + UtcJsonDtoContractRules.ScanContractTypes().Count;

        // Act
        var run = await EastOfUtcChildTestHost.RunAsync(rules, UtcJsonDtoContractGuard.ChildVariable);

        // Assert
        run.Exited.Should().BeTrue("the child test host should finish in time");
        run.ExitCode.Should().Be(0, "every UTC Z contract case should pass under TZ={0}:\n{1}",
            EastOfUtcChildTestHost.EastOfUtcZone, run.Output);
        run.Total.Should().NotBeNull("the child should report a test total:\n{0}", run.Output);
        run.Total.Should().Be(expectedCount, "the filter should select every case, not none");
        run.Skipped.Should().Be(0, "the child should run every case, including the zone check:\n{0}", run.Output);
    }
}
