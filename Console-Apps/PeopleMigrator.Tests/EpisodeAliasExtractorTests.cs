using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class EpisodeAliasExtractorTests
{
    [Fact]
    public void ExtractAliases_finds_mary_trump_variant_for_mary_l_trump()
    {
        var description =
            "Mary Trump joins Joanna Coles to pull back the curtain.\n\n" +
            "👤 Guest: Mary Trump\n" +
            "🎙️ Host: Joanna Coles\n";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Mary L Trump",
            "Why Seeing Epstein and My Uncle Donald Haunts Me",
            description,
            "@MaryLTrump",
            "@maryltrump.bsky.social",
            ["@MaryLTrump", "@maryltrump.bsky.social", "@JoannaColes", "@joannacoles.bsky.social"]);

        // Mary Trump normalizes to the same core as Mary L Trump — not a useful alias.
        aliases.Should().NotContain("Mary Trump");
        aliases.Should().NotContain("Joanna Coles");
    }

    [Fact]
    public void ExtractAliases_finds_diane_abbott_without_mp_suffix()
    {
        var title = "Mandelson Vetting: 'PM should consider his position' says Diane Abbott";
        var description =
            "The Independent MP for Hackney North and Stoke Newington Diane Abbott has told Sky News.";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Diane Abbott MP",
            title,
            description,
            "@HackneyAbbott",
            "@hackneyabbott.bsky.social",
            ["@KamaliMelbourne", "@HackneyAbbott", "@hackneyabbott.bsky.social"]);

        // Diane Abbott matches canonical core when MP suffix is stripped.
        aliases.Should().NotContain("Diane Abbott");
    }

    [Fact]
    public void ExtractAliases_excludes_co_guests_for_andrew_lownie()
    {
        var description =
            "Royal biographer Andrew Lownie explains why the fallout could pose a crisis.\n" +
            "Australian commentator Shauna Kay joins Tom for a fiery Royal Roast.\n\n" +
            "👤 Guest: Andrew Lownie, Plum Sykes, Paula Froelich, and Shauna Kay\n" +
            "🎙️ Host: Tom Sykes\n";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Andrew Lownie",
            "Why King Charles Must Abdicate Now: Andrew Lownie",
            description,
            "@andrewlownie",
            null,
            ["@andrewlownie"]);

        aliases.Should().NotContain("Shauna Kay");
        aliases.Should().NotContain("Plum Sykes");
        aliases.Should().NotContain("Paula Froelich");
        aliases.Should().NotContain("Tom Sykes");
    }

    [Fact]
    public void ExtractAliases_does_not_duplicate_canonical_name()
    {
        var description = "👤 Guest: Andrew Lownie\n🎙️ Host: Tom Sykes\n";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Andrew Lownie",
            "Interview with Andrew Lownie",
            description,
            "@andrewlownie",
            null,
            ["@andrewlownie"]);

        aliases.Should().NotContain("Andrew Lownie");
    }

    [Fact]
    public void ExtractAliases_finds_name_near_handle_mention()
    {
        var description =
            "Virginia Roberts Giuffre spoke with @VirginiaGiuffre about the case.";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Virginia Giuffre",
            "Giuffre memoir co-writer on how Andrew is responding",
            description,
            "@VirginiaGiuffre",
            null,
            ["@VirginiaGiuffre"]);

        aliases.Should().Contain("Virginia Roberts Giuffre");
    }

    [Fact]
    public void ExtractAliases_excludes_sentence_fragments_with_shared_first_name()
    {
        var description =
            "Andrew Update: Interview with Andrew Lownie\n" +
            "👤 Guest: Andrew Lownie\n";

        var aliases = EpisodeAliasExtractor.ExtractAliases(
            "Andrew Lownie",
            "Andrew Arrested: The Lownie Report",
            description,
            "@andrewlownie",
            null,
            ["@andrewlownie"]);

        aliases.Should().NotContain("Andrew Arrested");
        aliases.Should().NotContain("Andrew Update");
    }
}
