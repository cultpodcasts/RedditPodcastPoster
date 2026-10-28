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
    private const string SelfFileName = nameof(RemovedNullCosmosPredicateRules) + ".cs";
    private static readonly string[] SkippedFolders = [".git", ".vs", "bin", "obj", "node_modules"];

    // A 'not removed' predicate on Removed that tests IsDefined and false with no null disjunct, in either order.
    // Matched against whole files (\s spans newlines), so a predicate wrapped across lines is still caught.
    private static readonly Regex[] NotRemovedWithoutNull =
    [
        new(@"!\s*(?<v>\w+)\.Removed\.IsDefined\(\)\s*\|\|\s*\k<v>\.Removed\s*==\s*false\s*\)",
            RegexOptions.Compiled),
        new(@"\(\s*(?<v>\w+)\.Removed\s*==\s*false\s*\|\|\s*!\s*\k<v>\.Removed\.IsDefined\(\)\s*\)",
            RegexOptions.Compiled)
    ];

    [Fact(DisplayName =
        "No Cosmos LINQ 'not removed' predicate on a publisher drops a stored \"removed\": null: each one is " +
        "'undefined OR null OR false', because C# treats null as not removed and the two must agree (#1102).")]
    public void not_removed_predicates_include_null()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var offenders = SourceFiles(root)
            .SelectMany(file => Offenders(File.ReadAllText(file))
                .Select(line => $"{Path.GetRelativePath(root, file)}:{line}"))
            .ToList();

        // Assert
        offenders.Should().BeEmpty(
            "write '!x.Removed.IsDefined() || x.Removed == null || x.Removed == false' so a stored null counts as not removed");
    }

    [Theory(DisplayName =
        "The 'not removed' source guard catches a predicate without the null disjunct even when it is wrapped " +
        "across lines or written in reverse order, because the guard must not depend on formatting (#1102).")]
    [InlineData("x => (!x.Removed.IsDefined() || x.Removed == false) && x.IndexAllEpisodes")]
    [InlineData("podcast => (!podcast.Removed.IsDefined() ||\n                podcast.Removed == false) &&")]
    [InlineData("x => (x.Removed == false || !x.Removed.IsDefined())")]
    [InlineData("x => (x.Removed == false ||\r\n    !x.Removed.IsDefined())")]
    public void guard_catches_wrapped_and_reversed_predicates(string source)
    {
        // Arrange
        var text = "class C {\n" + source + "\n}";

        // Act
        var offenders = Offenders(text).ToList();

        // Assert
        offenders.Should().Equal(2);
    }

    [Theory(DisplayName =
        "The 'not removed' source guard accepts predicates that include the null disjunct, because those agree " +
        "with the C# rule (#1102).")]
    [InlineData("x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false)")]
    [InlineData("e => (!e.ParentRemoved.IsDefined() || e.ParentRemoved == false || e.ParentRemoved == null)")]
    [InlineData("x => (!x.Removed.IsDefined() || x.Removed == false || x.Removed == null)")]
    public void guard_accepts_predicates_with_null(string source)
    {
        // Act
        var offenders = Offenders(source).ToList();

        // Assert
        offenders.Should().BeEmpty();
    }

    private static IEnumerable<int> Offenders(string text) =>
        NotRemovedWithoutNull
            .SelectMany(regex => regex.Matches(text))
            .Select(match => text.AsSpan(0, match.Index).Count('\n') + 1)
            .Distinct()
            .Order();

    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            // This file holds offending samples on purpose (the guard's own tests).
            if (!Path.GetFileName(file).Equals(SelfFileName, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
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
