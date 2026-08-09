using Api.Models;
using Api.Services.People;
using FluentAssertions;
using RedditPodcastPoster.Models.People;
using Xunit;

namespace FunctionHost.Tests.Api;

/// <summary>
/// Pure-apply unit tests for <see cref="PersonChangeApplier"/>: mutates an in-memory
/// <see cref="Person"/> from a <see cref="PersonChangeRequest"/> with no repository I/O.
/// Covers server-side trim of name, aliases, and social handles on edit.
/// </summary>
public class PersonChangeApplierTests
{
    private static Person CreatePerson(Action<Person>? customize = null)
    {
        var person = new Person("Original Name")
        {
            Aliases = ["Original Alias"],
            TwitterHandle = "@originalx",
            BlueskyHandle = "@original.bsky.social"
        };
        customize?.Invoke(person);
        return person;
    }

    [Fact(DisplayName =
        "Apply trims leading and trailing whitespace from person name")]
    public void Apply_trims_name()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest { Name = "  Trimmed Name  " };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.Name.Should().Be("Trimmed Name");
    }

    [Fact(DisplayName =
        "Apply trims each alias and drops whitespace-only aliases")]
    public void Apply_trims_and_filters_aliases()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest
        {
            Aliases = ["  First Alias  ", "   ", "Second Alias", "\t"]
        };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.Aliases.Should().Equal("First Alias", "Second Alias");
    }

    [Fact(DisplayName =
        "Apply sets aliases to null when every supplied alias is whitespace")]
    public void Apply_whitespace_only_aliases_become_null()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest { Aliases = ["  ", "\t"] };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.Aliases.Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply trims each space-delimited Twitter/X handle and normalizes @ prefix")]
    public void Apply_trims_space_delimited_twitter_handles()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest
        {
            TwitterHandle = "  handle_one   @handle_two  "
        };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.TwitterHandle.Should().Be("@handle_one @handle_two");
    }

    [Fact(DisplayName =
        "Apply trims each space-delimited Bluesky handle and normalizes @ prefix")]
    public void Apply_trims_space_delimited_bluesky_handles()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest
        {
            BlueskyHandle = "  alice.bsky.social   @bob.bsky.social  "
        };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.BlueskyHandle.Should().Be("@alice.bsky.social @bob.bsky.social");
    }

    [Fact(DisplayName =
        "Apply sets Twitter handle to null when the supplied value is whitespace")]
    public void Apply_whitespace_only_twitter_handle_becomes_null()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest { TwitterHandle = "  " };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.TwitterHandle.Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply sets Bluesky handle to null when the supplied value is whitespace")]
    public void Apply_whitespace_only_bluesky_handle_becomes_null()
    {
        // Arrange
        var person = CreatePerson();
        var request = new PersonChangeRequest { BlueskyHandle = "\t" };

        // Act
        PersonChangeApplier.Apply(person, request);

        // Assert
        person.BlueskyHandle.Should().BeNull();
    }
}
