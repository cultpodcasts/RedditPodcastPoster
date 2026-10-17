using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

public class PublishOptionsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Publisher arguments: when --baseline is given as a separate value or with =, then the baseline path is " +
        "read, because update.ps1 passes the folder it moved the previous files to.")]
    public void baseline_path_is_read_from_either_form()
    {
        // Arrange
        var path = _fixture.Create<string>();

        // Act
        var separate = PublishOptions.Parse(["--baseline", path]);
        var joined = PublishOptions.Parse([$"--baseline={path}"]);

        // Assert
        separate.BaselinePath.Should().Be(path);
        joined.BaselinePath.Should().Be(path);
    }

    [Fact(DisplayName =
        "Publisher arguments: when --accept-unexplained-removals is present, then it is switched on without " +
        "needing a value, because it is a flag.")]
    public void accept_flag_needs_no_value()
    {
        // Arrange
        var path = _fixture.Create<string>();

        // Act
        var options = PublishOptions.Parse(["--accept-unexplained-removals", "--baseline", path]);

        // Assert
        options.AcceptUnexplainedRemovals.Should().BeTrue();
        options.BaselinePath.Should().Be(path);
    }

    [Fact(DisplayName =
        "Publisher arguments: when only the legacy --use-v2 argument is given, then the defaults apply, because " +
        "update.ps1 has always passed it and the publisher has always ignored it.")]
    public void legacy_argument_is_ignored()
    {
        // Arrange

        // Act
        var options = PublishOptions.Parse(["--use-v2"]);

        // Assert
        options.Should().Be(new PublishOptions());
    }

    [Fact(DisplayName =
        "Publisher arguments: when --baseline has no value, then parsing fails, because a missing baseline would " +
        "silently turn the removal check off.")]
    public void baseline_without_a_value_fails()
    {
        // Arrange

        // Act
        var parse = () => PublishOptions.Parse(["--baseline"]);

        // Assert
        parse.Should().Throw<ArgumentException>();
    }
}
