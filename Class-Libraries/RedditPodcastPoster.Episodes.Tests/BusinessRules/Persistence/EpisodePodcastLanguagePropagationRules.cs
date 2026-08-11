using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// HARD: Episode.ApplyPodcastDefaultLanguageChange for podcast API default changes.
/// See docs/episode-language.md.
/// </summary>
public class EpisodePodcastLanguagePropagationRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "INTEGRITY: when podcast default changes from fil to es, an episode still on fil moves to es, because it followed the previous default.")]
    public void apply_moves_episode_that_matched_previous_default()
    {
        // Arrange
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = "fil";

        // Act
        var changed = episode.ApplyPodcastDefaultLanguageChange("fil", "es");

        // Assert
        changed.Should().BeTrue();
        episode.Language.Should().Be("es");
    }

    [Fact(DisplayName =
        "INTEGRITY: when podcast default changes from fil to es, an English episode (null) stays null, because null is an English override not an unset inherit slot.")]
    public void apply_does_not_move_english_override()
    {
        // Arrange
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = null;

        // Act
        var changed = episode.ApplyPodcastDefaultLanguageChange("fil", "es");

        // Assert
        changed.Should().BeFalse();
        episode.Language.Should().BeNull();
    }

    [Fact(DisplayName =
        "INTEGRITY: when podcast default changes from English (null) to fil, null episodes move to fil, because they followed the English show default.")]
    public void apply_moves_english_default_followers_when_show_gets_non_english_default()
    {
        // Arrange
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = null;

        // Act
        var changed = episode.ApplyPodcastDefaultLanguageChange(null, "fil");

        // Assert
        changed.Should().BeTrue();
        episode.Language.Should().Be("fil");
    }

    [Fact(DisplayName =
        "INTEGRITY: when podcast default is cleared to English, episodes on the previous default become null.")]
    public void apply_clearing_default_to_english_nulls_previous_default_followers()
    {
        // Arrange
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = "fil";

        // Act
        var changed = episode.ApplyPodcastDefaultLanguageChange("fil", null);

        // Assert
        changed.Should().BeTrue();
        episode.Language.Should().BeNull();
    }
}
