using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Episodes.TestSupport.TimeZones;

/// <summary>The outcome of re-running one test class in a child <c>dotnet test</c> host.</summary>
/// <param name="Exited">False when the child was killed after the timeout.</param>
/// <param name="ExitCode">The child's exit code (0 when every selected test passed).</param>
/// <param name="Output">The child's stdout and stderr, for assertion messages.</param>
/// <param name="Total">The test total the child reported, or null when it reported none.</param>
/// <param name="Skipped">The skipped count the child reported (0 when it reported none).</param>
public sealed record ChildTestRun(bool Exited, int ExitCode, string Output, int? Total, int Skipped);

/// <summary>
/// Re-runs a test class in a child <c>dotnet test</c> of its own assembly with <c>TZ</c> set east of UTC (#1191,
/// #1194). CI runs on UTC, where an implicit host-local <see cref="DateTime"/> conversion applies a zero offset and
/// passes anyway; only a host east of UTC proves a rule ignores the host's zone. Linux and macOS only: Windows
/// ignores <c>TZ</c>.
/// </summary>
public static partial class EastOfUtcChildTestHost
{
    /// <summary>A zone east of UTC (NZST +12 / NZDT +13), so a host-local reading moves the instant by half a day.</summary>
    public const string EastOfUtcZone = "Pacific/Auckland";

    private static readonly TimeSpan ChildTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Runs every test in <paramref name="rules"/> in a child test host on <see cref="EastOfUtcZone"/> time, with
    /// <paramref name="childVariable"/> set to <c>1</c> so child-only facts run there.
    /// </summary>
    public static async Task<ChildTestRun> RunAsync(Type rules, string childVariable)
    {
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
        startInfo.Environment[childVariable] = "1";

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
        return new ChildTestRun(
            exited,
            child.ExitCode,
            output,
            total.Success ? int.Parse(total.Groups[1].Value) : null,
            skipped.Success ? int.Parse(skipped.Groups[1].Value) : 0);
    }

    /// <summary>True when <see cref="EastOfUtcZone"/> resolves; without tzdata the child falls back to UTC.</summary>
    public static bool EastOfUtcZoneResolves() => TimeZoneInfo.TryFindSystemTimeZoneById(EastOfUtcZone, out _);

    /// <summary>The SDK sets DOTNET_HOST_PATH for the processes <c>dotnet test</c> starts.</summary>
    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

    [GeneratedRegex(@"Total:\s+(\d+)")]
    private static partial Regex TotalPattern();

    [GeneratedRegex(@"Skipped:\s+(\d+)")]
    private static partial Regex SkippedPattern();
}
