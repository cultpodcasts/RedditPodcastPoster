using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Factories;

namespace RedditPodcastPoster.People.Tests;

public class PersonSortNameTests
{
    [Theory]
    [InlineData("Ilhan Omar", "Omar")]
    [InlineData("  Mary Smith-Jones  ", "Smith-Jones")]
    [InlineData("Madonna", "Madonna")]
    [InlineData("Jean-Luc Picard", "Picard")]
    [InlineData("A B C", "C")]
    public void DeriveSortKeyFromName_UsesLastWhitespaceToken(string name, string expected)
    {
        Person.DeriveSortKeyFromName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveSortKeyFromName_Blank_ReturnsEmpty(string? name)
    {
        Person.DeriveSortKeyFromName(name).Should().BeEmpty();
    }

    [Fact]
    public void GetEffectiveSortKey_NullSortName_DerivesLastToken()
    {
        Person.GetEffectiveSortKey("Ilhan Omar", null).Should().Be("Omar");
        Person.GetEffectiveSortKey("Ilhan Omar", "").Should().Be("Omar");
        Person.GetEffectiveSortKey("Ilhan Omar", "   ").Should().Be("Omar");
    }

    [Fact]
    public void GetEffectiveSortKey_ExplicitSortName_UsesOverride()
    {
        Person.GetEffectiveSortKey("Ilhan Omar", "Omar, Ilhan").Should().Be("Omar, Ilhan");
        Person.GetEffectiveSortKey("Something Else", "  Custom  ").Should().Be("Custom");
    }

    [Fact]
    public void GetEffectiveSortKey_OrganizationFullName_UsesFullName()
    {
        const string org = "Church of Scientology";
        Person.GetEffectiveSortKey(org, org).Should().Be(org);
    }

    [Fact]
    public void Instance_GetEffectiveSortKey_UsesPersistedSortName()
    {
        var person = new Person("Ilhan Omar") { SortName = null };
        person.GetEffectiveSortKey().Should().Be("Omar");

        person.SortName = "Church of Scientology";
        person.Name = "Church of Scientology";
        person.GetEffectiveSortKey().Should().Be("Church of Scientology");
    }

    [Fact]
    public void PersonFactory_Create_SetsSortName_WithoutChangingNameKey()
    {
        var person = new PersonFactory().Create(
            "  Ilhan Omar  ",
            sortName: "  Omar, Ilhan  ");

        person.Name.Should().Be("Ilhan Omar");
        person.NameKey.Should().Be("ilhan omar");
        person.SortName.Should().Be("Omar, Ilhan");
        person.GetEffectiveSortKey().Should().Be("Omar, Ilhan");
    }

    [Fact]
    public void PersonFactory_Create_BlankSortName_LeavesNull()
    {
        var person = new PersonFactory().Create("Ilhan Omar", sortName: "  ");

        person.SortName.Should().BeNull();
        person.GetEffectiveSortKey().Should().Be("Omar");
        person.NameKey.Should().Be("ilhan omar");
    }
}
