using FluentAssertions;
using RedditPodcastPoster.People.Factories;
using Xunit;

namespace RedditPodcastPoster.People.Tests;

/// <summary>
/// Create-path trim rules for person name, aliases, and social handles.
/// </summary>
public class PersonFactoryFieldTrimTests
{
    private readonly PersonFactory _factory = new();

    [Fact(DisplayName =
        "Create trims leading and trailing whitespace from person name")]
    public void Create_trims_name()
    {
        // Arrange
        // Act
        var person = _factory.Create("  Guest Name  ");

        // Assert
        person.Name.Should().Be("Guest Name");
    }

    [Fact(DisplayName =
        "Create trims each alias and drops whitespace-only aliases")]
    public void Create_trims_and_filters_aliases()
    {
        // Arrange
        // Act
        var person = _factory.Create(
            "Guest",
            aliases: ["  Aka One  ", "   ", "Aka Two"]);

        // Assert
        person.Aliases.Should().Equal("Aka One", "Aka Two");
    }

    [Fact(DisplayName =
        "Create sets aliases to null when every supplied alias is whitespace")]
    public void Create_whitespace_only_aliases_become_null()
    {
        // Arrange
        // Act
        var person = _factory.Create("Guest", aliases: ["  ", "\t"]);

        // Assert
        person.Aliases.Should().BeNull();
    }

    [Fact(DisplayName =
        "Create trims each space-delimited Twitter/X handle and normalizes @ prefix")]
    public void Create_trims_space_delimited_twitter_handles()
    {
        // Arrange
        // Act
        var person = _factory.Create(
            "Guest",
            twitterHandle: "  x_one   @x_two  ");

        // Assert
        person.TwitterHandle.Should().Be("@x_one @x_two");
    }

    [Fact(DisplayName =
        "Create trims each space-delimited Bluesky handle and normalizes @ prefix")]
    public void Create_trims_space_delimited_bluesky_handles()
    {
        // Arrange
        // Act
        var person = _factory.Create(
            "Guest",
            blueskyHandle: "  one.bsky.social   @two.bsky.social  ");

        // Assert
        person.BlueskyHandle.Should().Be("@one.bsky.social @two.bsky.social");
    }

    [Fact(DisplayName =
        "Create sets Twitter handle to null when the supplied value is whitespace")]
    public void Create_whitespace_only_twitter_handle_becomes_null()
    {
        // Arrange
        // Act
        var person = _factory.Create("Guest", twitterHandle: "  ");

        // Assert
        person.TwitterHandle.Should().BeNull();
    }

    [Fact(DisplayName =
        "Create sets Bluesky handle to null when the supplied value is whitespace")]
    public void Create_whitespace_only_bluesky_handle_becomes_null()
    {
        // Arrange
        // Act
        var person = _factory.Create("Guest", blueskyHandle: "\t");

        // Assert
        person.BlueskyHandle.Should().BeNull();
    }
}
