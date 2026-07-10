using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Factories;

namespace RedditPodcastPoster.People.Tests;

public class PersonNameKeyTests
{
    [Theory]
    [InlineData("Ilhan Omar", "ilhan omar")]
    [InlineData("  Ilhan Omar  ", "ilhan omar")]
    [InlineData("ILHAN OMAR", "ilhan omar")]
    [InlineData("ilhan omar", "ilhan omar")]
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
        var person = new Person("  Ilhan Omar  ");

        person.NameKey.Should().Be("ilhan omar");
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
        var person = new PersonFactory().Create("  Ilhan Omar  ");

        person.Name.Should().Be("Ilhan Omar");
        person.NameKey.Should().Be("ilhan omar");
    }

    [Fact]
    public void NameKeys_DifferingOnlyByCase_AreEqual()
    {
        var a = Person.NormalizeNameKey("Ilhan Omar");
        var b = Person.NormalizeNameKey("ilhan omar");

        a.Should().Be(b);
    }
}
