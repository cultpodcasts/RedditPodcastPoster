using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Factories;

namespace RedditPodcastPoster.People.Tests;

public class PersonNameKeyTests
{
    [Theory]
    [InlineData("Ada Example", "ada example")]
    [InlineData("  Ada Example  ", "ada example")]
    [InlineData("ADA EXAMPLE", "ada example")]
    [InlineData("ada example", "ada example")]
    public void NormalizeNameKey_TrimsAndLowercases(string input, string expected)
    {
        Person.NormalizeNameKey(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeNameKey_Blank_ReturnsEmpty(string? input)
    {
        Person.NormalizeNameKey(input).Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsNameKey()
    {
        var person = new Person("  Ada Example  ");

        person.NameKey.Should().Be("ada example");
    }

    [Fact]
    public void EnsureNameKey_UpdatesAfterRename()
    {
        var person = new Person("Alice");
        person.Name = "  Bob Smith  ";

        person.EnsureNameKey();

        person.NameKey.Should().Be("bob smith");
    }

    [Fact]
    public void PersonFactory_Create_SetsNameKey()
    {
        var person = new PersonFactory().Create("  Ada Example  ");

        person.Name.Should().Be("Ada Example");
        person.NameKey.Should().Be("ada example");
    }

    [Fact]
    public void NameKeys_DifferingOnlyByCase_AreEqual()
    {
        var a = Person.NormalizeNameKey("Ada Example");
        var b = Person.NormalizeNameKey("ada example");

        a.Should().Be(b);
    }
}
