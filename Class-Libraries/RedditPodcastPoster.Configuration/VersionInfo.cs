using System.Reflection;

namespace RedditPodcastPoster.Configuration;

public static class VersionInfo
{
    public static void PrintVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informationalVersion = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            Console.WriteLine(informationalVersion);
        }
        else
        {
            var version = assembly?.GetName().Version?.ToString() ?? "1.0.0";
            Console.WriteLine($"{assembly?.GetName().Name} {version}");
        }
    }
}
