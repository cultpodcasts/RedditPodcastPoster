using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Configuration.Tests.BusinessRules;

/// <summary>
///     Discovers the repository's console apps from their project files, so rules over "every CLI" cannot silently
///     miss a CLI that is added, renamed or moved.
/// </summary>
internal static class ConsoleAppSources
{
    private const string SolutionFileName = "RedditPodcastPoster.slnx";
    public const string EntryPointFileName = "Program.cs";

    /// <summary>
    ///     The Azure Function hosts (Api, Discovery, Indexer) are also <c>OutputType</c> Exe, but they are hosts, not
    ///     CLIs, and have no <c>--version</c> contract.
    /// </summary>
    private const string FunctionHostsFolder = "Cloud";

    private const string TestProjectSuffix = ".Tests";
    private const string ProjectFilePattern = "*.csproj";
    private const string SourceFilePattern = "*.cs";

    private static readonly string[] SkippedFolders = [".git", ".vs", "bin", "obj", "node_modules"];

    private static readonly Regex ExeOutputType =
        new(@"<OutputType>\s*Exe\s*</OutputType>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Repository-relative directories of every console app project (<c>OutputType</c> Exe).</summary>
    public static IReadOnlyList<string> ProjectDirectories() =>
        Files(ProjectFilePattern)
            .Where(project => ExeOutputType.IsMatch(File.ReadAllText(project)))
            .Select(project => RelativeDirectory(project))
            .Order()
            .ToList();

    /// <summary>Repository-relative directories that hold an entry point outside the Function hosts and tests.</summary>
    public static IReadOnlyList<string> EntryPointDirectories() =>
        Files(EntryPointFileName)
            .Select(entryPoint => RelativeDirectory(entryPoint))
            .Order()
            .ToList();

    public static IReadOnlyList<string> EntryPoints() =>
        ProjectDirectories()
            .Select(directory => Path.Combine(directory, EntryPointFileName))
            .ToList();

    public static IReadOnlyList<string> SourceFiles() =>
        ProjectDirectories()
            .SelectMany(directory => Files(SourceFilePattern, Path.Combine(RepositoryRoot, directory)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .Order()
            .ToList();

    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string RelativeDirectory(string file) =>
        Path.GetRelativePath(RepositoryRoot, Path.GetDirectoryName(file)!);

    private static IEnumerable<string> Files(string searchPattern) => Files(searchPattern, RepositoryRoot);

    private static IEnumerable<string> Files(string searchPattern, string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, searchPattern))
        {
            yield return file;
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(directory).Where(IsSearched))
        {
            foreach (var file in Files(searchPattern, subdirectory))
            {
                yield return file;
            }
        }
    }

    private static bool IsSearched(string directory)
    {
        var name = Path.GetFileName(directory);
        var isFunctionHosts = string.Equals(
            Path.GetRelativePath(RepositoryRoot, directory), FunctionHostsFolder, StringComparison.OrdinalIgnoreCase);
        return !isFunctionHosts &&
               !SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase) &&
               !name.EndsWith(TestProjectSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException($"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}
