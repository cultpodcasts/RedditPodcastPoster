using FluentAssertions;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Factories;

namespace RedditPodcastPoster.People.Tests;

public class PersonSortNameTests
{
    [Theory]
    [InlineData("Ada Example", "Example")]
    [InlineData("  Mary Smith-Jones  ", "Smith-Jones")]
    [InlineData("Solara", "Solara")]
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
        Person.GetEffectiveSortKey("Ada Example", null).Should().Be("Example");
        Person.GetEffectiveSortKey("Ada Example", "").Should().Be("Example");
        Person.GetEffectiveSortKey("Ada Example", "   ").Should().Be("Example");
    }

    [Fact]
    public void GetEffectiveSortKey_ExplicitSortName_UsesOverride()
    {
        Person.GetEffectiveSortKey("Ada Example", "Example, Ada").Should().Be("Example, Ada");
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
        var person = new Person("Ada Example") { SortName = null };
        person.GetEffectiveSortKey().Should().Be("Example");

        person.SortName = "Church of Scientology";
        person.Name = "Church of Scientology";
        person.GetEffectiveSortKey().Should().Be("Church of Scientology");
    }

    [Fact]
    public void PersonFactory_Create_SetsSortName_WithoutChangingNameKey()
    {
        var person = new PersonFactory().Create(
            "  Ada Example  ",
            sortName: "  Example, Ada  ");

        person.Name.Should().Be("Ada Example");
        person.NameKey.Should().Be("ada example");
        person.SortName.Should().Be("Example, Ada");
        person.IsOrganization.Should().BeFalse();
        person.GetEffectiveSortKey().Should().Be("Example, Ada");
    }

    [Fact]
    public void PersonFactory_Create_OrganizationFlag_PersistsFullNameSort()
    {
        var person = new PersonFactory().Create(
            "Mira Voss",
            sortName: "Voss",
            isOrganization: true);

        person.IsOrganization.Should().BeTrue();
        person.SortName.Should().Be("Mira Voss");
        person.GetEffectiveSortKey().Should().Be("Mira Voss");
        person.NameKey.Should().Be("mira voss");
    }

    [Fact]
    public void PersonFactory_Create_BlankSortName_LeavesNull()
    {
        var person = new PersonFactory().Create("Ada Example", sortName: "  ");

        person.SortName.Should().BeNull();
        person.GetEffectiveSortKey().Should().Be("Example");
        person.NameKey.Should().Be("ada example");
    }

    [Theory]
    [InlineData("CNN News Central", true)]
    [InlineData("Church of Scientology", true)]
    [InlineData("University of Michigan", true)]
    [InlineData("Ada Example", false)]
    [InlineData("Casey Compound Surname", false)]
    public void LooksLikeOrganization_DetectsOrgKeywords(string name, bool expected)
    {
        PersonSortNameResolver.LooksLikeOrganization(name).Should().Be(expected);
    }

    [Fact]
    public void ResolveForPersist_OmitsLastTokenDefault()
    {
        PersonSortNameResolver.ResolveForPersist("Ada Example", null).Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Ada Example", "Example").Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Ada Example", "  Example  ").Should().BeNull();
        PersonSortNameResolver.ResolveForPersist("Pat Placeholder", null).Should().BeNull();
    }

    [Fact]
    public void ResolveForPersist_ExplicitOrganizationFlag_UsesFullName_EvenWithoutHeuristic()
    {
        // Surname-style name: heuristic says not org, but curator flag forces full-name sort.
        PersonSortNameResolver.LooksLikeOrganization("Mira Voss").Should().BeFalse();
        PersonSortNameResolver.ResolveForPersist("Mira Voss", "Voss", isOrganization: true)
            .Should().Be("Mira Voss");
        PersonSortNameResolver.ResolveForPersist("Mira Voss", null, isOrganization: true)
            .Should().Be("Mira Voss");
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
        PersonSortNameResolver.GuessSortName("Ada Example").Should().Be("Example");
        PersonSortNameResolver.GuessSortName("Solara").Should().Be("Solara");
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
        PersonSortNameResolver.ResolveForPersist("Casey Compound Surname", "Compound Surname")
            .Should().Be("Compound Surname");
    }
}
