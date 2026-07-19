using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People.Tests;

public class PersonGetNamesTests
{
    [Fact]
    public void GetNames_YieldsTrimmedName()
    {
        var person = new Person("  Ada Example  ");

        person.GetNames().Should().Equal("Ada Example");
    }

    [Fact]
    public void GetNames_YieldsTrimmedNameAndAliases()
    {
        var person = new Person("Ada Example")
        {
            Aliases = ["  Ada  ", "Example", "  ", null!]
        };

        person.GetNames().Should().Equal("Ada Example", "Ada", "Example");
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
