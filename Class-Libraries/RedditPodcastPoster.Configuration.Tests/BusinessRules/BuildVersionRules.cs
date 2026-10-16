using System.Reflection;
using FluentAssertions;

namespace RedditPodcastPoster.Configuration.Tests.BusinessRules;

public class BuildVersionRules
{
    private const string DirtyLinePrefix = "Dirty:";
    private const char SourceRevisionSeparator = '+';

    [Fact(DisplayName =
        "The build's informational version ends at the Dirty line, because the SDK's +<commit> suffix repeats the Git Hash line and corrupts the Dirty value.")]
    public void Informational_version_has_no_source_revision_suffix()
    {
        // Arrange
        var informationalVersion = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        // Act
        var dirtyLine = informationalVersion
            .Split('\n')
            .Single(line => line.StartsWith(DirtyLinePrefix, StringComparison.Ordinal));

        // Assert
        dirtyLine.Should().NotContain(SourceRevisionSeparator.ToString());
    }
}
