using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace ConsoleApps.Composition.Tests;

public class ConsoleAppUserSecretsRules
{
    [Fact(DisplayName =
        "Every console app that reads user secrets uses the same UserSecretsId, because the shared store holds the " +
        "Cosmos, api and Auth0 settings; a console with its own id starts without them (#1464).")]
    public void console_apps_share_one_user_secrets_id()
    {
        // Arrange
        var consoleApps = Path.Combine(FindRepoRoot(), "Console-Apps");

        // Act
        var ids = Directory.EnumerateFiles(consoleApps, "*.csproj", SearchOption.AllDirectories)
            .Select(path => (Project: Path.GetFileNameWithoutExtension(path),
                Id: XDocument.Load(path).Descendants("UserSecretsId").Select(e => e.Value.Trim()).FirstOrDefault()))
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToArray();

        // Assert
        ids.Should().Contain(p => p.Project == "D1SearchReconcile");
        ids.Select(p => p.Id).Distinct().Should().ContainSingle(
            "these projects must share one store: {0}",
            string.Join(", ", ids.Select(p => $"{p.Project}={p.Id}")));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RedditPodcastPoster.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
