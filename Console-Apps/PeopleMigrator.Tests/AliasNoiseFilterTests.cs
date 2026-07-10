using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class AliasNoiseFilterTests
{
    [Theory]
    [InlineData("Jerry Wise Jerry Wise")]
    [InlineData("Tina Brown Tina Brown")]
    public void HasDuplicatedPhrase_detects_repeated_halves(string alias)
    {
        AliasNoiseFilter.HasDuplicatedPhrase(alias).Should().BeTrue();
    }

    [Theory]
    [InlineData("Andrew Lownie's Substack", "Andrew Lownie")]
    [InlineData("Beth Granger's Facebook", "Beth Granger")]
    [InlineData("Reid Meloy Books Dr", "Reid Meloy")]
    [InlineData("Marci Shore Dr", "Marci Shore")]
    [InlineData("Alice Hines Links", "Alice Hines")]
    public void IsNoiseAlias_detects_platform_and_fragment_suffixes(string alias, string canonical)
    {
        AliasNoiseFilter.IsNoiseAlias(alias, canonical).Should().BeTrue();
    }

    [Fact]
    public void IsNoiseAlias_detects_location_prefix_before_canonical()
    {
        AliasNoiseFilter.IsNoiseAlias("Stoke Newington Diane Abbott", "Diane Abbott MP").Should().BeTrue();
    }

    [Fact]
    public void IsNoiseAlias_detects_sentence_fragment_phrases()
    {
        AliasNoiseFilter.IsNoiseAlias("Dr Lucy Sixsmith Breaks Down", "Lucy Sixsmith").Should().BeTrue();
        AliasNoiseFilter.ContainsPhraseNoise("Connection Links Mentioned Beth Granger").Should().BeTrue();
    }

    [Fact]
    public void IsNoiseAlias_keeps_unrelated_alias()
    {
        AliasNoiseFilter.IsNoiseAlias("Alex Stein", "Alexandra Stein").Should().BeFalse();
    }
}
