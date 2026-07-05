using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Adapters;

/// <summary>
/// Layer 1 rules — catalogue candidate factory preserves platform episode shape at provider boundaries.
/// </summary>
public class EpisodeFromCandidateFactoryRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeFromCandidateFactory _factory = new();

    [Fact(DisplayName =
        "When a Spotify catalogue candidate is materialized, the episode matches the legacy FromSpotify shape " +
        "because provider boundaries must not change indexed episode fields.")]
    public void Spotify_catalogue_candidate_materializes_to_legacy_episode_shape()
    {
        // Arrange
        var input = _fixture.CreateSpotifyCatalogueInput();
        var expected = _fixture.CreateSpotifyCatalogueEpisode(
            input.SpotifyId,
            title: input.Title,
            spotifyUrl: input.SpotifyUrl,
            release: input.Release,
            length: input.Duration,
            description: input.Description,
            image: input.Image);
        var candidate = new SpotifyEpisodeAdapter().Adapt(input);

        // Act
        var episode = _factory.Create(candidate, false);

        // Assert
        episode.ShouldMatchExpectation(EpisodeExpectation.From(expected));
    }

    [Fact(DisplayName =
        "When an Apple catalogue candidate is materialized, the episode matches the legacy FromApple shape " +
        "because provider boundaries must not change indexed episode fields.")]
    public void Apple_catalogue_candidate_materializes_to_legacy_episode_shape()
    {
        // Arrange
        var input = _fixture.CreateAppleCatalogueInput();
        var expected = _fixture.CreateAppleCatalogueEpisode(
            input.AppleId,
            title: input.Title,
            release: input.Release,
            length: input.Duration,
            description: input.Description,
            appleUrl: input.AppleUrl);
        var candidate = new AppleEpisodeAdapter().Adapt(input);

        // Act
        var episode = _factory.Create(candidate, false);

        // Assert
        episode.ShouldMatchExpectation(EpisodeExpectation.From(expected));
    }

    [Fact(DisplayName =
        "When a YouTube catalogue candidate is materialized, the episode matches the legacy FromYouTube shape " +
        "because provider boundaries must not change indexed episode fields.")]
    public void YouTube_catalogue_candidate_materializes_to_legacy_episode_shape()
    {
        // Arrange
        var input = _fixture.CreateYouTubeCatalogueInput();
        var expected = _fixture.CreateYouTubeCatalogueEpisode(
            input.YouTubeId,
            title: input.Title,
            release: input.Release,
            length: input.Duration,
            description: input.Description,
            youTubeUrl: input.YouTubeUrl,
            image: input.Image);
        var candidate = new YouTubeEpisodeAdapter().Adapt(input);

        // Act
        var episode = _factory.Create(candidate, false);

        // Assert
        episode.ShouldMatchExpectation(EpisodeExpectation.From(expected));
    }

    [Fact(DisplayName =
        "When explicit content is set on materialization, the episode carries the explicit flag " +
        "because catalogue APIs expose explicit separately from candidate core fields.")]
    public void Explicit_flag_is_applied_on_materialization()
    {
        // Arrange
        var input = _fixture.CreateSpotifyCatalogueInput();
        var candidate = new SpotifyEpisodeAdapter().Adapt(input);

        // Act
        var episode = _factory.Create(candidate, explicitContent: true);

        // Assert
        episode.Explicit.Should().BeTrue();
    }
}
