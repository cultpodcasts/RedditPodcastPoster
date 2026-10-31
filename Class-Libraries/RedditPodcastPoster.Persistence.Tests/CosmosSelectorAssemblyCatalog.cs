using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Finds every <see cref="CosmosSelector"/> type the file-key guards must inspect, without a hand-kept assembly list.
/// Types come from every assembly in the test output that is, or references, the Models assembly.
/// <see cref="UnscannedProductionProjects"/> cross-checks that against the source tree, so a production project that
/// declares a CosmosSelector subclass but is not referenced by this test project fails the guard instead of being skipped.
/// </summary>
internal static class CosmosSelectorAssemblyCatalog
{
    private static readonly Lazy<IReadOnlyList<Assembly>> LazyAssemblies = new(LoadAssemblies);

    public static IReadOnlyList<Assembly> Assemblies => LazyAssemblies.Value;

    /// <summary>Every CosmosSelector subclass (abstract included) in <see cref="Assemblies"/>.</summary>
    public static IReadOnlyList<Type> AllSelectorTypes() =>
        Assemblies.SelectMany(LoadableTypes)
            .Where(t => t.IsClass && t != typeof(CosmosSelector) && typeof(CosmosSelector).IsAssignableFrom(t))
            .Distinct()
            .ToList();

    public static IReadOnlyList<Type> ConcreteSelectorTypes() =>
        AllSelectorTypes().Where(t => !t.IsAbstract).ToList();

    /// <summary>
    /// Production projects (not test or test-support projects) whose source declares a class deriving from
    /// CosmosSelector or a known subclass of it, and whose assembly is not in <see cref="Assemblies"/>.
    /// </summary>
    public static IReadOnlyList<string> UnscannedProductionProjects(out IReadOnlyList<string> declaringProjects)
    {
        var root = FindRepositoryRoot();
        var baseNames = AllSelectorTypes().Append(typeof(CosmosSelector)).Select(SourceName)
            .ToHashSet(StringComparer.Ordinal);
        var scanned = Assemblies.Select(a => a.GetName().Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var declaring = new List<string>();
        var missing = new List<string>();
        foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                     .Where(p => !IsBuildOutput(root, p) && !IsTestProject(p)))
        {
            var projectDir = Path.GetDirectoryName(project)!;
            var declares = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(projectDir, f) && NearestProject(f) == project)
                .Any(f => DeclaresSubclassOf(File.ReadAllText(f), baseNames));
            if (!declares)
            {
                continue;
            }

            var assemblyName = AssemblyNameOf(project);
            declaring.Add(assemblyName);
            if (!scanned.Contains(assemblyName))
            {
                missing.Add(Path.GetRelativePath(root, project));
            }
        }

        declaringProjects = declaring;
        return missing;
    }

    private static IReadOnlyList<Assembly> LoadAssemblies()
    {
        var modelsName = typeof(CosmosSelector).Assembly.GetName().Name!;
        var assemblies = new List<Assembly>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            if (!IsOrReferences(path, modelsName))
            {
                continue;
            }

            assemblies.Add(Assembly.Load(AssemblyName.GetAssemblyName(path)));
        }

        return assemblies;
    }

    private static bool IsOrReferences(string path, string assemblyName)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
            {
                return false;
            }

            var reader = pe.GetMetadataReader();
            if (!reader.IsAssembly)
            {
                return false;
            }

            if (reader.GetString(reader.GetAssemblyDefinition().Name) == assemblyName)
            {
                return true;
            }

            return reader.AssemblyReferences
                .Select(h => reader.GetString(reader.GetAssemblyReference(h).Name))
                .Contains(assemblyName, StringComparer.Ordinal);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }

    private static readonly Regex ClassHeader = new(@"\bclass\s+\w+[^{;]*", RegexOptions.Compiled);
    private static readonly Regex Parenthesised = new(@"\([^()]*\)", RegexOptions.Compiled);
    private static readonly Regex Comments = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Whether <paramref name="source"/> declares a class whose base list names one of <paramref name="baseFullNames"/>
    /// (full names as written in C#, e.g. <c>RedditPodcastPoster.Models.Cosmos.CosmosSelector</c>). An unqualified base
    /// matches by simple name, as in-repo code writes it. A qualified base must be a suffix of a full name on whole
    /// segments, so a third-party <c>SpotifyAPI.Web.Episode</c> does not count as our <c>Episode</c>.
    /// </summary>
    internal static bool DeclaresSubclassOf(string source, IReadOnlySet<string> baseFullNames)
    {
        foreach (Match header in ClassHeader.Matches(Comments.Replace(source, " ")))
        {
            var text = header.Value;
            while (Parenthesised.IsMatch(text))
            {
                text = Parenthesised.Replace(text, " ");
            }

            var whereAt = Regex.Match(text, @"\bwhere\b");
            if (whereAt.Success)
            {
                text = text[..whereAt.Index];
            }

            var colon = text.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var bases = Regex.Replace(text[(colon + 1)..], "<[^<>]*>", " ")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(b => b.Replace("global::", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal));
            if (bases.Any(b => NamesKnownBase(b, baseFullNames)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The written base equals a known full name or is a whole-segment suffix of one. Unqualified names are a
    /// one-segment suffix, so they match by simple name; a using-imported third-party type of the same simple name
    /// still matches (fails loud, documented in the class-header theory), as resolving usings needs a compiler.
    /// </summary>
    private static bool NamesKnownBase(string written, IReadOnlySet<string> baseFullNames) =>
        baseFullNames.Any(full => full == written || full.EndsWith("." + written, StringComparison.Ordinal));

    /// <summary>A type's full name as C# source writes it: no generic arity, nested types joined by a dot.</summary>
    internal static string SourceName(Type type)
    {
        var name = (type.FullName ?? type.Name).Replace('+', '.');
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RedditPodcastPoster.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"RedditPodcastPoster.slnx not found above {AppContext.BaseDirectory}; the file-key guard needs the source tree.");
    }

    private static bool IsBuildOutput(string root, string path) =>
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(s => s is "bin" or "obj" or "node_modules" or ".git");

    private static string? NearestProject(string file)
    {
        for (var dir = Path.GetDirectoryName(file); dir != null; dir = Path.GetDirectoryName(dir))
        {
            var project = Directory.EnumerateFiles(dir, "*.csproj").FirstOrDefault();
            if (project != null)
            {
                return project;
            }
        }

        return null;
    }

    private static bool IsTestProject(string project)
    {
        var name = Path.GetFileNameWithoutExtension(project);
        if (name.EndsWith("Tests", StringComparison.Ordinal) || name.Contains("TestSupport", StringComparison.Ordinal))
        {
            return true;
        }

        return XDocument.Load(project).Descendants("IsTestProject")
            .Any(e => string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
    }

    private static string AssemblyNameOf(string project) =>
        XDocument.Load(project).Descendants("AssemblyName").Select(e => e.Value.Trim())
            .FirstOrDefault(v => v.Length > 0) ?? Path.GetFileNameWithoutExtension(project);
}
