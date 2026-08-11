using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// HARD integrity rules for read-time episode language and podcast-default following.
/// See docs/episode-language.md.
/// </summary>
public class EpisodeLanguageResolutionRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "INTEGRITY: when an episode document is present and Language is null (product English) on a non-English podcast, " +
        "ForRead returns null and must not yield the podcast language, because null means English and coalescing to " +
        "podcast.Language corrupts English subject search and enrichment.")]
    public void for_read_null_episode_language_is_english_not_podcast_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = null;

        // Act
        var resolved = EpisodeLanguageResolution.ForRead(podcast, episode);

        // Assert
        resolved.Should().BeNull(
            "null Episode.Language is English; returning podcast Language would be the forbidden coalesce");
        resolved.Should().NotBe(podcast.Language);
    }

    [Fact(DisplayName =
        "INTEGRITY: when an episode has an explicit non-English Language, ForRead returns that code even if the podcast " +
        "language differs, because episode language is authoritative at read time.")]
    public void for_read_prefers_explicit_episode_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = "es";

        // Act
        var resolved = EpisodeLanguageResolution.ForRead(podcast, episode);

        // Assert
        resolved.Should().Be("es");
    }

    [Fact(DisplayName =
        "When no episode document is supplied, ForRead uses Podcast.Language, because podcast-only paths have no episode lang.")]
    public void for_read_without_episode_uses_podcast_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";

        // Act
        var resolved = EpisodeLanguageResolution.ForRead(podcast, episode: null);

        // Assert
        resolved.Should().Be("fil");
    }

    [Fact(DisplayName =
        "INTEGRITY: ForEpisode returns Episode.Language unchanged including null, because callers must not substitute podcast language.")]
    public void for_episode_returns_episode_language_including_null()
    {
        // Arrange
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = null;

        // Act
        var resolved = EpisodeLanguageResolution.ForEpisode(episode);

        // Assert
        resolved.Should().BeNull();
    }

    [Fact(DisplayName =
        "INTEGRITY: when the podcast default is non-English, a null episode language does not follow that default, " +
        "because null means English override — podcast API language changes must not stamp the new default onto it.")]
    public void follows_default_false_for_english_override_on_non_english_show()
    {
        // Arrange
        // Act
        var follows = EpisodeLanguageResolution.FollowsPodcastDefault(
            episodeLanguage: null,
            podcastDefaultLanguage: "fil");

        // Assert
        follows.Should().BeFalse();
    }

    [Fact(DisplayName =
        "INTEGRITY: when the podcast default is non-English, an episode whose Language equals that default follows it, " +
        "so a podcast language change moves those episodes to the new default.")]
    public void follows_default_true_when_episode_matches_non_english_default()
    {
        // Arrange
        // Act
        var follows = EpisodeLanguageResolution.FollowsPodcastDefault("fil", "fil");

        // Assert
        follows.Should().BeTrue();
    }

    [Fact(DisplayName =
        "INTEGRITY: when the podcast default is English (null), a null episode language follows that default, " +
        "so setting a non-English podcast language moves those episodes onto the new default.")]
    public void follows_default_true_when_both_english()
    {
        // Arrange
        // Act
        var follows = EpisodeLanguageResolution.FollowsPodcastDefault(null, null);

        // Assert
        follows.Should().BeTrue();
    }

    [Fact(DisplayName =
        "INTEGRITY: LanguageAfterPodcastDefaultChange moves followers from the previous non-English default to the new code " +
        "and leaves English overrides (null) unchanged.")]
    public void language_after_change_moves_followers_leaves_english_override()
    {
        // Arrange
        // Act
        var moved = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange("fil", "fil", "es");
        var englishOverride = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange(null, "fil", "es");
        var otherOverride = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange("pt", "fil", "es");

        // Assert
        moved.Should().Be("es");
        englishOverride.Should().BeNull();
        otherOverride.Should().Be("pt");
    }

    [Fact(DisplayName =
        "INTEGRITY: LanguageAfterPodcastDefaultChange when clearing the podcast default to English sets followers to null " +
        "and leaves unrelated overrides alone.")]
    public void language_after_change_to_english_nulls_followers()
    {
        // Arrange
        // Act
        var moved = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange("fil", "fil", null);
        var englishAlready = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange(null, "fil", null);

        // Assert
        moved.Should().BeNull();
        englishAlready.Should().BeNull();
    }
}
