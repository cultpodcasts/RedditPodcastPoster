using System.Reflection;

namespace RedditPodcastPoster.Configuration;

public static class VersionInfo
{
    public const string VersionArgument = "--version";
    public const string DefaultVersion = "1.0.0";

    /// <summary>
    ///     Answers a <c>--version</c> request. Call first in every console app's entry point, before any host,
    ///     configuration or data access is built. Prints the version block once to standard output and returns
    ///     <c>true</c> when the caller should exit with code 0; prints nothing and returns <c>false</c> otherwise.
    /// </summary>
    public static bool TryHandleVersionRequest(string[] args) =>
        TryHandleVersionRequest(args, Console.Out, Assembly.GetEntryAssembly());

    public static bool TryHandleVersionRequest(string[] args, TextWriter output, Assembly? assembly)
    {
        if (!args.Contains(VersionArgument, StringComparer.Ordinal))
        {
            return false;
        }

        PrintVersion(output, assembly);
        return true;
    }

    private static void PrintVersion(TextWriter output, Assembly? assembly)
    {
        var name = assembly?.GetName().Name;
        var informationalVersion = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            output.WriteLine($"{name} {informationalVersion}");
        }
        else
        {
            var version = assembly?.GetName().Version?.ToString() ?? DefaultVersion;
            output.WriteLine($"{name} {version}");
        }
    }
}
