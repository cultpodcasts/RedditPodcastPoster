using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace ConsoleApps.Composition.Tests;

public class D1SearchReconcileConfigRules
{
    [Fact(DisplayName =
        "The D1SearchReconcile console keeps non-secret defaults in its appsettings.json and does not put " +
        "ClientId, ClientSecret or Enabled there, because user secrets are only for the search-index-writer " +
        "credentials and Enabled is the Functions switch (#1464).")]
    public void console_appsettings_hold_non_secret_defaults_only()
    {
        // Arrange
        var path = Path.Combine(FindRepoRoot(), "Console-Apps", "D1SearchReconcile", "appsettings.json");

        // Act
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var names = FlattenNames(json.RootElement).ToArray();

        // Assert
        json.RootElement.GetProperty("d1Search").GetProperty("Reconcile").GetProperty("DriftThreshold").GetDouble()
            .Should().Be(0.05);
        names.Should().NotContain("ClientId");
        names.Should().NotContain("ClientSecret");
        names.Should().NotContain("Enabled");
        names.Should().NotContain("Endpoint");
        names.Should().NotContain("Auth0Domain");
        names.Should().NotContain("Audience");
    }

    [Fact(DisplayName =
        "The D1SearchReconcile console copies its appsettings.json to the output, because non-secret defaults such as " +
        "the drift threshold must be present when the app runs.")]
    public void console_csproj_copies_appsettings_to_the_output()
    {
        // Arrange
        var path = Path.Combine(FindRepoRoot(), "Console-Apps", "D1SearchReconcile", "D1SearchReconcile.csproj");

        // Act
        var appsettings = XDocument.Load(path).Descendants("None")
            .Single(e => (string?)e.Attribute("Update") == "appsettings.json");

        // Assert
        appsettings.Element("CopyToOutputDirectory")!.Value.Should().Be("Always");
    }

    [Fact(DisplayName =
        "The D1SearchReconcile console loads appsettings.json and D1SearchReconcile.appsettings.json from the exe " +
        "directory, because publish-console-apps.ps1 copies the file as D1SearchReconcile.appsettings.json into " +
        "artifacts/tools.")]
    public void console_loads_published_appsettings_from_the_exe_directory()
    {
        // Arrange
        var path = Path.Combine(FindRepoRoot(), "Console-Apps", "D1SearchReconcile", "Program.cs");

        // Act
        var text = File.ReadAllText(path);

        // Assert
        text.Should().Contain("AppContext.BaseDirectory");
        text.Should().Contain("D1SearchReconcile.appsettings.json");
        text.Should().Contain("appsettings.json");
    }

    private static IEnumerable<string> FlattenNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var child in FlattenNames(property.Value))
                    {
                        yield return child;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in FlattenNames(item))
                    {
                        yield return child;
                    }
                }
                break;
        }
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
