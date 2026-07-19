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

    [Theory]
    [InlineData("CNN News Central", true)]
    [InlineData("Church of Scientology", true)]
    [InlineData("University of Michigan", true)]
    [InlineData("Ilhan Omar", false)]
    [InlineData("Daniella Mestyanek Young", false)]
    public void LooksLikeOrganization_DetectsOrgKeywords(string name, bool expected)
    {
        PersonSortNameResolver.LooksLikeOrganization(name).Should().Be(expected);
    }

    [Fact]
    public void ResolveForPersist_OmitsLastTokenDefault()
    {
        PersonSortNameResolver.ResolveForPersist("Ilhan Omar", null).Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Ilhan Omar", "Omar").Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Ilhan Omar", "  Omar  ").Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Alan Sherry", null).Should().BeNull();
    }

    [Fact]
    public void ResolveForPersist_KeepsOrgFullName_EvenWhenSeedOmitted()
    {
        PersonSortNameResolver.ResolveForPersist("CNN News Central", null)
            .Should().Be("CNN News Central");
        PersonSortNameResolver.ResolveForPersist("CNN News Central", "CNN News Central")
            .Should().Be("CNN News Central");
    }

    [Fact]
    public void ResolveForPersist_StripsLeadingThe_ForOrgNames()
    {
        PersonSortNameResolver.StripLeadingThe("The Lead CNN").Should().Be("Lead CNN");
        PersonSortNameResolver.StripLeadingThe("the BBC").Should().Be("BBC");
        PersonSortNameResolver.StripLeadingThe("The New York Times").Should().Be("New York Times");

        PersonSortNameResolver.GuessSortName("The Lead CNN").Should().Be("Lead CNN");
        PersonSortNameResolver.ResolveForPersist("The Lead CNN", null).Should().Be("Lead CNN");
        PersonSortNameResolver.ResolveForPersist("The Lead CNN", "The Lead CNN")
            .Should().Be("Lead CNN");
        PersonSortNameResolver.ResolveForPersist("The Lead CNN", "Lead CNN")
            .Should().Be("Lead CNN");
    }

    [Fact]
    public void GuessSortName_Person_UsesLastToken()
    {
        PersonSortNameResolver.GuessSortName("Ilhan Omar").Should().Be("Omar");
        PersonSortNameResolver.GuessSortName("Madonna").Should().Be("Madonna");
    }

    [Fact]
    public void StripLeadingThe_NonThePrefix_Unchanged()
    {
        PersonSortNameResolver.StripLeadingThe("Lead CNN").Should().Be("Lead CNN");
        PersonSortNameResolver.StripLeadingThe("Theater Group").Should().Be("Theater Group");
    }

    [Fact]
    public void ResolveForPersist_KeepsManualOverride()
    {
        PersonSortNameResolver.ResolveForPersist("Daniella Mestyanek Young", "Mestyanek Young")
            .Should().Be("Mestyanek Young");
    }
}
