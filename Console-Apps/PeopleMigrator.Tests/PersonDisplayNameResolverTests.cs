using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class PersonDisplayNameResolverTests
{
    [Fact]
    public void ChooseBestDisplayName_prefers_real_name_over_handle_like_values()
    {
        var chosen = PersonDisplayNameResolver.ChooseBestDisplayName(
            "Emily Maitlis",
            "Emily Maitlis",
            "@maitlis",
            "@maitlis.bsky.social");

        chosen.Should().Be("Emily Maitlis");
    }

    [Theory]
    [InlineData("maitlis", "@maitlis", "@maitlis.bsky.social")]
    [InlineData("Maitlis", "@maitlis", null)]
    [InlineData("@maitlis", "@maitlis", null)]
    public void IsUsableDisplayName_rejects_handle_like_names(string name, string? twitter, string? bluesky)
    {
        PersonDisplayNameResolver.IsUsableDisplayName(name, twitter, bluesky).Should().BeFalse();
    }

    [Fact]
    public void IsUsableDisplayName_accepts_multi_word_names_when_first_word_matches_short_handle()
    {
        PersonDisplayNameResolver.IsUsableDisplayName("John Smith", "@john", null).Should().BeTrue();
    }

    [Fact]
    public void ChooseBestDisplayName_prefers_name_with_space_when_sources_disagree()
    {
        var chosen = PersonDisplayNameResolver.ChooseBestDisplayName(
            "Emily",
            "Emily Maitlis",
            "@maitlis",
            "@maitlis.bsky.social");

        chosen.Should().Be("Emily Maitlis");
    }

    [Fact]
    public void ChooseBestDisplayName_prefers_clean_name_over_parenthetical_social_handle()
    {
        var chosen = PersonDisplayNameResolver.ChooseBestDisplayName(
            PersonDisplayNameResolver.NormalizeResolvedDisplayName(
                "Eliza Anyangwe (elizatalks.bsky.social)"),
            PersonDisplayNameResolver.NormalizeResolvedDisplayName("Eliza Anyangwe"),
            "@ElizaTalks",
            "@elizatalks.bsky.social");

        chosen.Should().Be("Eliza Anyangwe");
    }

    [Fact]
    public void ResolveChosenSource_prefers_twitter_when_both_match()
    {
        PersonDisplayNameResolver.ResolveChosenSource("Ana Cabrera", "Ana Cabrera", "Ana Cabrera")
            .Should().Be("twitter");
    }
}

public class XProfileDisplayNameParserTests
{
    [Theory]
    [InlineData(
        """<title>Anderson Cooper (@andersoncooper) / X</title>""",
        "andersoncooper",
        "Anderson Cooper")]
    [InlineData(
        """<meta property="og:title" content="Alex Witt (@AlexWitt) on X">""",
        "AlexWitt",
        "Alex Witt")]
    public void ParseDisplayName_reads_ssr_profile_title(string html, string handle, string expected)
    {
        XProfileDisplayNameParser.ParseDisplayName(html, handle).Should().Be(expected);
    }
}
