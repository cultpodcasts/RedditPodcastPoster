using System.Text.RegularExpressions;
using FluentAssertions;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1102: "not removed" must mean the same thing in Cosmos as in C#. <c>Publisher.Removed</c> (podcasts, TV shows,
/// news organisations) is a <c>bool?</c> and every C# reader treats null as not removed (<c>IsRemoved()</c> is
/// <c>Removed == true</c>). In Cosmos <c>null = false</c> is undefined, so a LINQ predicate written as
/// <c>!x.Removed.IsDefined() || x.Removed == false</c> silently drops a stored <c>"removed": null</c>.
/// </summary>
public class RemovedNullCosmosPredicateRules
{
    private const string SolutionFileName = "RedditPodcastPoster.slnx";
    private static readonly string[] SkippedFolders = [".git", ".vs", "bin", "obj", "node_modules"];

    // A 'not removed' predicate on Removed that tests IsDefined and false but has no null disjunct in between.
    private static readonly Regex NotRemovedWithoutNull = new(
        @"!\s*(?<v>\w+)\.Removed\.IsDefined\(\)\s*\|\|\s*\k<v>\.Removed\s*==\s*false\s*\)",
        RegexOptions.Compiled);

    [Fact(DisplayName =
        "No Cosmos LINQ 'not removed' predicate on a publisher drops a stored \"removed\": null: each one is " +
        "'undefined OR null OR false', because C# treats null as not removed and the two must agree (#1102).")]
    public void not_removed_predicates_include_null()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var offenders = SourceFiles(root)
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (line, index))
                .Where(x => NotRemovedWithoutNull.IsMatch(x.line))
                .Select(x => $"{Path.GetRelativePath(root, file)}:{x.index + 1}"))
            .ToList();

        // Assert
        offenders.Should().BeEmpty(
            "write '!x.Removed.IsDefined() || x.Removed == null || x.Removed == false' so a stored null counts as not removed");
    }

    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedFolders.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in SourceFiles(child))
            {
                yield return file;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"{SolutionFileName} not found.");
    }
}
