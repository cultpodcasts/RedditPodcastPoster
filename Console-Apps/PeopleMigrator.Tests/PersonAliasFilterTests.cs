using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Factories;
using Xunit;

namespace PeopleMigrator.Tests;

public class PersonAliasFilterTests
{
    [Fact]
    public void WouldMatchCanonical_excludes_honorific_and_credential_variants()
    {
        PersonAliasFilter.WouldMatchCanonical("Alexandra Stein", "Dr Alexandra Stein").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Alexandra Stein", "Dr. Alexandra Stein").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Diane Abbott MP", "Diane Abbott").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Mary L Trump", "Mary Trump").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Christine Marie, PhD", "Christine Marie").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Jerry Wise MA, MS, CLC", "Jerry Wise").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Daniel Shaw LCSW", "Daniel Shaw").Should().BeTrue();
        PersonAliasFilter.WouldMatchCanonical("Jamie Marich LMHC", "Jamie Marich").Should().BeTrue();
    }

    [Fact]
    public void WouldMatchCanonical_keeps_maiden_name_middle_token()
    {
        PersonAliasFilter.WouldMatchCanonical("Virginia Giuffre", "Virginia Roberts Giuffre").Should().BeFalse();
    }

    [Fact]
    public void WouldMatchCanonical_keeps_genuine_nicknames()
    {
        PersonAliasFilter.WouldMatchCanonical("Alexandra Stein", "Alex Stein").Should().BeFalse();
        PersonAliasFilter.WouldMatchCanonical("Alexander Stein", "Alex Stein").Should().BeFalse();
    }

    [Fact]
    public void FilterAliases_excludes_honorific_variants()
    {
        var filtered = PersonAliasFilter.FilterAliases(
            ["Dr Alexandra Stein", "Alex Stein"],
            "Alexandra Stein");

        filtered.Should().Equal("Alex Stein");
    }

    [Fact]
    public void FilterAliases_excludes_middle_initial_variant()
    {
        var filtered = PersonAliasFilter.FilterAliases(
            ["Mary Trump", "Mary L Trump"],
            "Mary L Trump");

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void FilterAliases_excludes_alias_that_matches_canonical_name()
    {
        var filtered = PersonAliasFilter.FilterAliases(
            ["Andrew Lownie", "Andrew Lownie Books"],
            "Andrew Lownie");

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void FilterAliases_excludes_prose_noise_aliases()
    {
        var filtered = PersonAliasFilter.FilterAliases(
            [
                "Jerry Wise Jerry Wise",
                "Tina Brown Tina Brown",
                "Andrew Lownie's Substack",
                "Stoke Newington Diane Abbott",
                "Reid Meloy Dr",
                "Reid Meloy Books Dr",
                "Marci Shore Dr"
            ],
            "Jerry Wise",
            twitterHandle: "@jerrywise");

        filtered.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Andrew Lownie", "andrew lownie")]
    [InlineData("E.J. Smith", "Ej Smith")]
    [InlineData("Mary-Jane Watson", "Mary Jane Watson")]
    public void IsSameName_treats_normalized_names_as_equal(string left, string right)
    {
        PersonAliasFilter.IsSameName(left, right).Should().BeTrue();
    }

    [Fact]
    public void FilterAliases_excludes_handle_derived_name_when_canonical_is_set()
    {
        var filtered = PersonAliasFilter.FilterAliases(
            ["Andrewlownie"],
            "Andrew Lownie",
            twitterHandle: "@andrewlownie");

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void BuildAliasesForPerson_does_not_include_canonical_name()
    {
        var person = new PersonFactory().Create(
            "Andrew Lownie",
            ["Andrew Lownie"],
            "@andrewlownie",
            null);

        var aliases = PersonAliasFilter.BuildAliasesForPerson(person);

        aliases.Should().BeEmpty();
    }

    [Fact]
    public void MergeAliases_does_not_store_canonical_name_as_alias()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());
        var person = registry.Resolve("@andrewlownie", null).Person;
        person.Name = "Andrew Lownie";

        registry.ApplyDescriptionExtract(
            "@andrewlownie",
            null,
            "Andrew Lownie",
            ["Andrew Lownie", "Andy Lownie"],
            Guid.NewGuid());

        person.Aliases.Should().Equal("Andy Lownie");
    }
}

public class PeopleSeedJsonWriterTests
{
    [Fact]
    public void BuildAliasesForPerson_excludes_self_for_andrew_lownie()
    {
        var person = new PersonFactory().Create(
            "Andrew Lownie",
            ["Andrew Lownie"],
            "@andrewlownie",
            null);

        var aliases = PersonAliasFilter.BuildAliasesForPerson(person);

        aliases.Should().NotContain("Andrew Lownie");
        aliases.Should().BeEmpty();
    }
}
