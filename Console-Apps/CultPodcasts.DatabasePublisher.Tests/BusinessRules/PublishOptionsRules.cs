using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

public class PublishOptionsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Publisher arguments: when --initial is present, then it is switched on without needing a value, because " +
        "it is the explicit statement that the output folder holds no previous publish.")]
    public void initial_is_a_flag()
    {
        // Arrange

        // Act
        var options = PublishOptions.Parse(["--initial"]);

        // Assert
        options.Initial.Should().BeTrue();
        options.Accepted.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publisher arguments: when --accept-removal is repeated as a separate value or with =, then every value is " +
        "kept, because each reviewed removal is accepted on its own.")]
    public void accept_removal_is_repeatable_in_either_form()
    {
        // Arrange
        var first = _fixture.Create<string>();
        var second = _fixture.Create<string>();

        // Act
        var options = PublishOptions.Parse(["--accept-removal", first, $"--accept-removal={second}"]);

        // Assert
        options.Accepted.Should().Equal(first, second);
        options.Initial.Should().BeFalse();
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
        options.Initial.Should().BeFalse();
        options.Accepted.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publisher arguments: when --accept-removal has no value or the next token is another option, then " +
        "parsing fails, because a swallowed option would accept something nobody named.")]
    public void accept_removal_without_a_value_fails()
    {
        // Arrange

        // Act
        var missing = () => PublishOptions.Parse(["--accept-removal"]);
        var swallowed = () => PublishOptions.Parse(["--accept-removal", "--initial"]);
        var empty = () => PublishOptions.Parse(["--accept-removal="]);

        // Assert
        missing.Should().Throw<ArgumentException>();
        swallowed.Should().Throw<ArgumentException>();
        empty.Should().Throw<ArgumentException>();
    }
}
