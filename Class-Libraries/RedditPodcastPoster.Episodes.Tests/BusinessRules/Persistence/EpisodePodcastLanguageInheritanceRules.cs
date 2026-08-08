using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

public class EpisodePodcastLanguageInheritanceRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "SetPodcastProperties with inheritLanguageIfUnset: when episode Language is unset and podcast has a language, then episode Language becomes the podcast language, because podcast default fills unset episode langs.")]
    public void set_podcast_properties_inherits_language_when_episode_lang_unset()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PodcastLanguage = null;
            })
            .Create();

        // Act
        var (updated, _) = episode.SetPodcastProperties(podcast, inheritLanguageIfUnset: true);

        // Assert
        updated.Should().BeTrue();
        episode.Language.Should().Be("fil");
        episode.PodcastLanguage.Should().Be("fil");
    }

    [Fact(DisplayName =
        "SetPodcastProperties with inheritLanguageIfUnset: when episode already has an explicit Language, then that Language is left alone, because curator/episode overrides must not be overwritten by podcast default.")]
    public void set_podcast_properties_does_not_overwrite_explicit_episode_language()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = "es";
                e.PodcastLanguage = null;
            })
            .Create();

        // Act
        var (updated, _) = episode.SetPodcastProperties(podcast, inheritLanguageIfUnset: true);

        // Assert
        updated.Should().BeTrue();
        episode.Language.Should().Be("es");
        episode.PodcastLanguage.Should().Be("fil");
    }

    [Fact(DisplayName =
        "SetPodcastProperties with inheritLanguageIfUnset: when podcast Language is empty, then episode Language stays unset, because clearing the podcast default must not invent an episode language.")]
    public void set_podcast_properties_does_not_invent_language_when_podcast_lang_empty()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = null);
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PodcastLanguage = "fil";
            })
            .Create();

        // Act
        var (updated, _) = episode.SetPodcastProperties(podcast, inheritLanguageIfUnset: true);

        // Assert
        updated.Should().BeTrue();
        episode.Language.Should().BeNull();
        episode.PodcastLanguage.Should().BeNull();
    }
}
