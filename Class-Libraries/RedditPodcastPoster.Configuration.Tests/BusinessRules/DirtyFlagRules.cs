using System.Diagnostics;
using System.Xml.Linq;
using FluentAssertions;

namespace RedditPodcastPoster.Configuration.Tests.BusinessRules;

public class DirtyFlagRules : IDisposable
{
    private const string PathspecProperty = "GitDirtyPathspec";
    private const string ProjectFolder = "Console-Apps/SomeApp";
    private const string GitExecutable = "git";
    private const string GitIdentity = "-c user.name=rules -c user.email=rules@example.invalid";

    private readonly string _repository = Path.Combine(Path.GetTempPath(), $"dirty-flag-{Guid.NewGuid():N}");
    private readonly string _projectDirectory;

    public DirtyFlagRules()
    {
        _projectDirectory = Path.Combine(_repository, ProjectFolder);
        Directory.CreateDirectory(_projectDirectory);
        File.WriteAllText(Path.Combine(_projectDirectory, "Program.cs"), "// tracked");
        Git("init --quiet");
        Git("add --all");
        Git($"{GitIdentity} commit --quiet --message initial");
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_repository, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_repository, true);
    }

    [Fact(DisplayName =
        "A freshly committed tree is not dirty, because the build matches the commit.")]
    public void Committed_tree_is_clean()
    {
        // Arrange
        // (the constructor commits the tree)

        // Act
        var status = DirtyStatus();

        // Assert
        status.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "An untracked Markdown file anywhere does not make the build dirty, because documentation is never a build input.")]
    [InlineData("README.md")]
    [InlineData("docs/note.md")]
    [InlineData("Console-Apps/SomeApp/notes.mdx")]
    public void Untracked_markdown_is_not_dirty(string relativePath)
    {
        // Arrange
        Write(relativePath);

        // Act
        var status = DirtyStatus();

        // Assert
        status.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "An untracked file that is not Markdown makes the build dirty, because new sources are compiled and JSON contracts and baselines are read by tests and gates.")]
    [InlineData("Console-Apps/SomeApp/Extra.cs")]
    [InlineData("docs/contracts/contract.json")]
    [InlineData("plans/baseline.json")]
    [InlineData("scripts/check.ps1")]
    public void Untracked_non_markdown_is_dirty(string relativePath)
    {
        // Arrange
        Write(relativePath);

        // Act
        var status = DirtyStatus();

        // Assert
        status.Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "An edit to a tracked source file makes the build dirty, because the build no longer matches the commit.")]
    public void Edited_tracked_source_is_dirty()
    {
        // Arrange
        File.AppendAllText(Path.Combine(_projectDirectory, "Program.cs"), "// edited");

        // Act
        var status = DirtyStatus();

        // Assert
        status.Should().NotBeEmpty();
    }

    private string DirtyStatus()
    {
        // The pathspec is read from Directory.Build.targets, the one place MSBuild takes it from, and run from a
        // project directory, as MSBuild's Exec does.
        var pathspec = XDocument.Load(Path.Combine(ConsoleAppSources.RepositoryRoot, "Directory.Build.targets"))
            .Descendants(PathspecProperty)
            .Single()
            .Value;
        return Git($"status --porcelain -- {pathspec}", _projectDirectory);
    }

    private void Write(string relativePath)
    {
        var path = Path.Combine(_repository, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "untracked");
    }

    private string Git(string arguments, string? workingDirectory = null)
    {
        var start = new ProcessStartInfo(GitExecutable, arguments)
        {
            WorkingDirectory = workingDirectory ?? _repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, "git {0} failed: {1}", arguments, error);
        return output.Trim();
    }
}
