using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People.Tests;

public class PersonGetNamesTests
{
    [Fact]
    public void GetNames_YieldsTrimmedName()
    {
        var person = new Person("  Ilhan Omar  ");

        person.GetNames().Should().Equal("Ilhan Omar");
    }

    [Fact]
    public void GetNames_YieldsTrimmedNameAndAliases()
    {
        var person = new Person("Ilhan Omar")
        {
            Aliases = ["  Ilhan  ", "Omar", "  ", null!]
        };

        person.GetNames().Should().Equal("Ilhan Omar", "Ilhan", "Omar");
    }

    [Fact]
    public void GetNames_BlankName_YieldsOnlyAliases()
    {
        var person = new Person("x") { Name = "   ", Aliases = ["Alias"] };
        person.EnsureNameKey();

        person.GetNames().Should().Equal("Alias");
    }

    [Fact]
    public void GetNames_NullAliases_YieldsOnlyName()
    {
        var person = new Person("Alice") { Aliases = null };

        person.GetNames().Should().Equal("Alice");
    }
}
