using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Makes <see cref="SearchReleaseUtcRules"/> bite on a UTC host such as CI (#1191). It re-runs that class in a child
/// <c>dotnet test</c> of this assembly with TZ set east of UTC, where reading the release as host-local time throws or
/// moves it. No workflow step and no reflection on <see cref="TimeZoneInfo.Local"/> are needed. Linux and macOS honour
/// TZ; on Windows the child falls back to the machine's own zone.
/// </summary>
public partial class SearchReleaseEastOfUtcHostGuard
{
    private const string EastOfUtcZone = "Pacific/Auckland";
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromMinutes(3);

    [Fact(DisplayName =
        "When the search release rules are re-run in a child test host on Pacific/Auckland time, then every one of them runs and passes, because CI runs on UTC where an implicit host-local release conversion would still pass, so only a host east of UTC proves the release ignores the host's zone.")]
    public void search_release_rules_pass_on_a_host_east_of_utc()
    {
        // Arrange
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

        startInfo.Environment["TZ"] = EastOfUtcZone;

        // Act
        using var child = Process.Start(startInfo)!;
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        var exited = child.WaitForExit(ChildTimeout);
        if (!exited)
        {
            child.Kill(true);
        }

        var output = stdout.Result + stderr.Result;
        var total = TotalPattern().Match(output);

        // Assert
        exited.Should().BeTrue("the child test host should finish within {0}", ChildTimeout);
        child.ExitCode.Should().Be(0, "every search release rule should pass under TZ={0}:\n{1}", EastOfUtcZone, output);
        total.Success.Should().BeTrue("the child should report a test total:\n{0}", output);
        int.Parse(total.Groups[1].Value).Should().Be(expectedCount, "the filter should select every rule, not none");
    }

    /// <summary>The SDK sets DOTNET_HOST_PATH for the processes <c>dotnet test</c> starts.</summary>
    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

    [GeneratedRegex(@"Total:\s+(\d+)")]
    private static partial Regex TotalPattern();
}
