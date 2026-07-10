using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class PersonNameMatcherTests
{
    [Theory]
    [InlineData("Mary Trump", "Mary L Trump", true)]
    [InlineData("Diane Abbott", "Diane Abbott MP", true)]
    [InlineData("Virginia Giuffre", "Virginia Roberts Giuffre", true)]
    [InlineData("Andrew Lownie", "Shauna Kay", false)]
    [InlineData("Andrew Lownie", "Plum Sykes", false)]
    public void FuzzyMatchCanonical_matches_name_variants(string candidate, string canonical, bool expected)
    {
        PersonNameMatcher.FuzzyMatchCanonical(candidate, canonical).Should().Be(expected);
    }

    [Fact]
    public void NameRelatesToPerson_matches_handle_or_canonical()
    {
        PersonNameMatcher.NameRelatesToPerson(
                "Mary Trump",
                "Mary L Trump",
                "@MaryLTrump",
                "@maryltrump.bsky.social")
            .Should().BeTrue();

        PersonNameMatcher.NameRelatesToPerson(
                "Shauna Kay",
                "Andrew Lownie",
                "@andrewlownie",
                null)
            .Should().BeFalse();
    }

    [Fact]
    public void NameRelatesToPerson_rejects_unrelated_shared_surname()
    {
        PersonNameMatcher.NameRelatesToPerson(
                "Donald Trump",
                "Mary L Trump",
                "@MaryLTrump",
                "@maryltrump.bsky.social")
            .Should().BeFalse();

        PersonNameMatcher.NameRelatesToPerson(
                "Prince Andrew",
                "Andrew Lownie",
                "@andrewlownie",
                null)
            .Should().BeFalse();
    }
}
