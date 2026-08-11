using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// HARD integrity rules for read-time episode language. Deviation = corruption of search /
/// enrichment language handling. See docs/episode-language.md.
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
}
