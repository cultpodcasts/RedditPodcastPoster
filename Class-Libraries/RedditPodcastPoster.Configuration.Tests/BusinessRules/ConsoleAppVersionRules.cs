using System.Text.RegularExpressions;
using FluentAssertions;

namespace RedditPodcastPoster.Configuration.Tests.BusinessRules;

public class ConsoleAppVersionRules
{
    private const string VersionGuard = "if (VersionInfo.TryHandleVersionRequest(args))";
    private const string ParserVersionSignal = "VersionRequestedError";

    private static readonly Regex UsingDirective =
        new(@"^(global\s+)?using\s+(static\s+)?([\w.]+\s*=\s*)?[\w.<>]+;$", RegexOptions.Compiled);

    private static readonly Regex VersionOption =
        new(@"\[Option\(\s*(\x27\w\x27\s*,\s*)?""version""", RegexOptions.Compiled);

    public static TheoryData<string> ConsoleAppEntryPoints()
    {
        var data = new TheoryData<string>();
        foreach (var entryPoint in ConsoleAppSources.EntryPoints())
        {
            data.Add(entryPoint);
        }

        return data;
    }

    public static TheoryData<string> ConsoleAppSourceFiles()
    {
        var data = new TheoryData<string>();
        foreach (var source in ConsoleAppSources.SourceFiles())
        {
            data.Add(source);
        }

        return data;
    }

    [Fact(DisplayName =
        "Every console app project has an entry point and every entry point belongs to a console app project, because a CLI without a checked Program.cs is a CLI whose --version is unchecked.")]
    public void Console_app_projects_and_entry_points_are_the_same_set()
    {
        // Arrange
        var projectDirectories = ConsoleAppSources.ProjectDirectories();

        // Act
        var entryPointDirectories = ConsoleAppSources.EntryPointDirectories();

        // Assert
        projectDirectories.Should().NotBeEmpty();
        entryPointDirectories.Should().BeEquivalentTo(projectDirectories);
    }

    [Theory(DisplayName =
        "A console app answers --version as its first statement, because --version must print once and never build a host, read config or touch Cosmos or Search.")]
    [MemberData(nameof(ConsoleAppEntryPoints))]
    public void Console_app_answers_version_before_anything_else(string entryPoint)
    {
        // Arrange
        var firstStatement = FirstStatement(ConsoleAppSources.Read(entryPoint));

        // Act
        var answersVersionFirst = firstStatement.StartsWith(VersionGuard, StringComparison.Ordinal);

        // Assert
        answersVersionFirst.Should().BeTrue(
            "{0} must start with `{1}` but starts with `{2}`", entryPoint, VersionGuard, firstStatement);
    }

    [Theory(DisplayName =
        "A console app does not react to the argument parser's version signal, because the guard has already printed the version and a second print makes the block appear twice.")]
    [MemberData(nameof(ConsoleAppEntryPoints))]
    public void Console_app_does_not_handle_the_parser_version_signal(string entryPoint)
    {
        // Arrange
        var source = ConsoleAppSources.Read(entryPoint);

        // Act
        var handlesParserSignal = source.Contains(ParserVersionSignal, StringComparison.Ordinal);

        // Assert
        handlesParserSignal.Should().BeFalse("{0} must not handle {1}", entryPoint, ParserVersionSignal);
    }

    [Theory(DisplayName =
        "A console app does not declare its own version option, because the shared guard owns --version and the parser's built-in version text must not be shadowed.")]
    [MemberData(nameof(ConsoleAppSourceFiles))]
    public void Console_app_does_not_declare_a_version_option(string sourceFile)
    {
        // Arrange
        var source = ConsoleAppSources.Read(sourceFile);

        // Act
        var declaresVersionOption = VersionOption.IsMatch(source);

        // Assert
        declaresVersionOption.Should().BeFalse("{0} must not declare a version option", sourceFile);
    }

    private static string FirstStatement(string source) =>
        source
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal) && !UsingDirective.IsMatch(line))
        ?? string.Empty;
}

