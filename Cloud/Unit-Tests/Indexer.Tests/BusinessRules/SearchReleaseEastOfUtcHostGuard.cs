using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Makes <see cref="SearchReleaseUtcRules"/> bite on a UTC host such as CI (#1191). It re-runs that class in a child
/// <c>dotnet test</c> of this assembly with TZ set east of UTC, where reading the release as host-local time throws or
/// moves it. No workflow step and no reflection on <see cref="TimeZoneInfo.Local"/> are needed. The child also runs
/// <see cref="SearchReleaseUtcRules.child_host_is_on_pacific_auckland_time"/>, so a silent fall-back to UTC (no tzdata)
/// fails instead of passing. Skipped on Windows, which ignores TZ.
/// </summary>
public partial class SearchReleaseEastOfUtcHostGuard
{
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromMinutes(3);

    [TzHonouringHostFact(DisplayName =
        "When the search release rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where an implicit host-local release conversion would still pass, so only a host east of UTC proves the release ignores the host's zone.")]
    public async Task search_release_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
        TimeZoneInfo.TryFindSystemTimeZoneById(SearchReleaseGuard.EastOfUtcZone, out _).Should().BeTrue(
            "the {0} zone must resolve, or the child falls back to UTC and proves nothing; install tzdata",
            SearchReleaseGuard.EastOfUtcZone);
        var rules = typeof(SearchReleaseUtcRules);
        var expectedCount = rules.GetMethods().Count(m => m.IsDefined(typeof(FactAttribute), true));
        var startInfo = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 {
                     "test", rules.Assembly.Location, "--filter", $"FullyQualifiedName~{rules.FullName}.", "--nologo"
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["TZ"] = SearchReleaseGuard.EastOfUtcZone;
        startInfo.Environment[SearchReleaseGuard.ChildVariable] = "1";

        // Act
        using var child = Process.Start(startInfo)!;
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        var exited = true;
        using (var timeout = new CancellationTokenSource(ChildTimeout))
        {
            try
            {
                await child.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                exited = false;
                child.Kill(true);
                await child.WaitForExitAsync();
            }
        }

        var output = await stdout + await stderr;
        var total = TotalPattern().Match(output);
        var skipped = SkippedPattern().Match(output);

        // Assert
        exited.Should().BeTrue("the child test host should finish within {0}", ChildTimeout);
        child.ExitCode.Should().Be(0, "every search release rule should pass under TZ={0}:\n{1}",
            SearchReleaseGuard.EastOfUtcZone, output);
        total.Success.Should().BeTrue("the child should report a test total:\n{0}", output);
        int.Parse(total.Groups[1].Value).Should().Be(expectedCount, "the filter should select every rule, not none");
        (skipped.Success ? int.Parse(skipped.Groups[1].Value) : 0).Should().Be(0,
            "the child should run every rule, including the zone check:\n{0}", output);
    }

    /// <summary>The SDK sets DOTNET_HOST_PATH for the processes <c>dotnet test</c> starts.</summary>
    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

    [GeneratedRegex(@"Total:\s+(\d+)")]
    private static partial Regex TotalPattern();

    [GeneratedRegex(@"Skipped:\s+(\d+)")]
    private static partial Regex SkippedPattern();
}
