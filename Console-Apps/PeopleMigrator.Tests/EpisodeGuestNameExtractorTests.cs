using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class EpisodeGuestNameExtractorTests
{
    [Fact]
    public void ExtractForEpisode_reads_guest_and_host_lines()
    {
        var description =
            "David Rothkopf joins Joanna Coles to argue.\n\n" +
            "👤 Guest: David Rothkopf\n" +
            "🎙️ Host: Joanna Coles\n";

        var results = EpisodeGuestNameExtractor.ExtractForEpisode(
            "Why Epstein Is Trump’s Defining Crime: Rothkopf",
            description,
            ["@JoannaColes"],
            ["@joannacoles.bsky.social", "@djrothkopf.bsky.social"]);

        results.Should().Contain(x =>
            x.Handle.Equals("@joannacoles.bsky.social", StringComparison.OrdinalIgnoreCase) &&
            x.DisplayName == "Joanna Coles");
        results.Should().Contain(x =>
            x.Handle.Equals("@djrothkopf.bsky.social", StringComparison.OrdinalIgnoreCase) &&
            x.DisplayName == "David Rothkopf");
    }

    [Fact]
    public void ExtractForEpisode_reads_title_says_pattern()
    {
        var results = EpisodeGuestNameExtractor.ExtractForEpisode(
            "Epstein came to my office and told me to stop publishing stories, says Tina Brown",
            "Tina Brown reveals Jeffrey Epstein came to her office.",
            ["@TinaBrownLM"],
            ["@tinabrownlm.bsky.social"]);

        results.Should().Contain(x =>
            x.DisplayName == "Tina Brown" &&
            x.Handle.Contains("tinabrown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractForEpisode_reads_interview_title_pattern()
    {
        var results = EpisodeGuestNameExtractor.ExtractForEpisode(
            "Jamie Marich, PhD interviews Janja Lalich, PhD. Cults and Complex Trauma",
            "Jamie Marich, PhD speaks internationally. Janja Lalich, Ph.D. is a researcher.",
            ["@LalichJanja"],
            ["@jlalich.bsky.social"]);

        results.Should().Contain(x =>
            x.DisplayName == "Janja Lalich" &&
            x.Handle.Contains("lalich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractForEpisode_splits_multiple_guests_from_role_line()
    {
        var description =
            "👤 Guest: Michael Wolff, Stacey Williams, Cleo Glyde and Tina Brown\n" +
            "🎙️ Host: Joanna Coles\n";

        var results = EpisodeGuestNameExtractor.ExtractForEpisode(
            "Why Epstein's Shadow Still Haunts Trump",
            description,
            ["@MichaelWolffNYC", "@TinaBrownLM"],
            ["@michaelwolffnyc.bsky.social", "@tinabrownlm.bsky.social"]);

        results.Should().Contain(x =>
            x.Handle.Equals("@MichaelWolffNYC", StringComparison.OrdinalIgnoreCase) &&
            x.DisplayName == "Michael Wolff");
        results.Should().Contain(x =>
            x.Handle.Equals("@tinabrownlm.bsky.social", StringComparison.OrdinalIgnoreCase) &&
            x.DisplayName == "Tina Brown");
    }

    [Theory]
    [InlineData("Tina Brown", "@TinaBrownLM", 25)]
    [InlineData("Michael Wolff", "@MichaelWolffNYC", 100)]
    [InlineData("Janja Lalich", "@LalichJanja", 50)]
    public void ScoreNameAgainstHandle_matches_name_parts_to_handle_token(string name, string handle, int expectedMinimum)
    {
        EpisodeGuestNameExtractor.ScoreNameAgainstHandle(name, handle).Should().BeGreaterThanOrEqualTo(expectedMinimum);
    }

    [Fact]
    public void NormalizePersonName_strips_credentials_and_pronouns()
    {
        EpisodeGuestNameExtractor.NormalizePersonName("Janja Lalich, Ph.D.").Should().Be("Janja Lalich");
        EpisodeGuestNameExtractor.NormalizePersonName("Jamie Marich, PhD (she/they)").Should().Be("Jamie Marich");
        EpisodeGuestNameExtractor.NormalizePersonName("Daniel Shaw LCSW").Should().Be("Daniel Shaw");
        EpisodeGuestNameExtractor.NormalizePersonName("Alex Rivera, LMHC").Should().Be("Alex Rivera");
        EpisodeGuestNameExtractor.NormalizePersonName("Eliza Anyangwe (elizatalks.bsky.social)")
            .Should().Be("Eliza Anyangwe");
        EpisodeGuestNameExtractor.NormalizePersonName("Melissa Murray (@ProfMMurray on Threads)")
            .Should().Be("Melissa Murray");
        EpisodeGuestNameExtractor.NormalizePersonName("International Cultic Studies Association (ICSA)")
            .Should().Be("International Cultic Studies Association (ICSA)");
        EpisodeGuestNameExtractor.NormalizePersonName("Rep. Suhas Subramanyam (VA-10)")
            .Should().Be("Rep. Suhas Subramanyam (VA-10)");
    }
}
