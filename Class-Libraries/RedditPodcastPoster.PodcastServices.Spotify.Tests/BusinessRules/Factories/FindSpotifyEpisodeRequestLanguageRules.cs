using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Factories;

/// <summary>
/// HARD integrity: Spotify find requests must not coalesce episode lang to podcast lang.
/// See docs/episode-language.md.
/// </summary>
public class FindSpotifyEpisodeRequestLanguageRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "INTEGRITY: when Create(Podcast, Episode) builds a find request and Episode.Language is null (English) on a non-English podcast, " +
        "the request Language must stay null, because coalescing to podcast.Language would load the wrong language ignored-subjects during catalogue matching.")]
    public void create_from_episode_keeps_null_language_as_english()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.Language = null;
        episode.PodcastId = podcast.Id;

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, episode);

        // Assert
        request.Language.Should().BeNull();
        request.Language.Should().NotBe(podcast.Language);
    }

    [Fact(DisplayName =
        "When Create(Podcast, Episode) builds a find request and Episode.Language is set, that code is passed through.")]
    public void create_from_episode_passes_explicit_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.Language = "es";
        episode.PodcastId = podcast.Id;

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, episode);

        // Assert
        request.Language.Should().Be("es");
    }
}
