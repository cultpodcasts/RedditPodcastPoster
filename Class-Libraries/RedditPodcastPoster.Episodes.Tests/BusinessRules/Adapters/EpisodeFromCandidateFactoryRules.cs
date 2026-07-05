using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Domain;
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

    public static TheoryData<CataloguePlatform> AllCataloguePlatforms() =>
        new()
        {
            CataloguePlatform.Spotify,
            CataloguePlatform.Apple,
            CataloguePlatform.YouTube
        };

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

    [Theory(DisplayName =
        "When a catalogue candidate has no artwork, materialization leaves episode images unset " +
        "because legacy factories only set images when artwork is present.")]
    [MemberData(nameof(AllCataloguePlatforms))]
    public void Null_artwork_leaves_episode_images_unset(CataloguePlatform platform)
    {
        // Arrange
        var candidate = CreateCandidate(platform, includeArtwork: false);

        // Act
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.Images.Should().BeNull();
    }

    [Theory(DisplayName =
        "When a catalogue candidate includes artwork, materialization sets the matching platform image " +
        "because indexed episodes carry per-platform artwork.")]
    [MemberData(nameof(AllCataloguePlatforms))]
    public void Artwork_sets_matching_platform_image(CataloguePlatform platform)
    {
        // Arrange
        var candidate = CreateCandidate(platform, includeArtwork: true);
        var expected = EpisodeExpectation.From(candidate);

        // Act
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.ShouldMatchExpectation(expected);
    }

    [Theory(DisplayName =
        "When explicit content is false on materialization, the episode is not marked explicit " +
        "because the flag is supplied separately from candidate core fields.")]
    [MemberData(nameof(AllCataloguePlatforms))]
    public void Explicit_false_is_preserved_on_materialization(CataloguePlatform platform)
    {
        // Arrange
        var candidate = CreateCandidate(platform, includeArtwork: false);

        // Act
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.Explicit.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a candidate has no source link, materialization copies title, description, duration, and release only " +
        "because platform fields come exclusively from the link.")]
    public void Null_source_link_materializes_core_fields_only()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var duration = _fixture.CreateDuration();
        var release = _fixture.CreateAppleCatalogueInput().Release;
        var candidate = new EpisodeCandidate(
            title,
            description,
            duration,
            new ReleaseInfo(release, ReleasePrecision.DateTimeUtc),
            SourceLink: null);

        // Act
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.Title.Should().Be(title);
        episode.Description.Should().Be(description);
        episode.Length.Should().Be(duration);
        episode.Release.Should().Be(release);
        episode.SpotifyId.Should().BeNullOrEmpty();
        episode.AppleId.Should().BeNull();
        episode.YouTubeId.Should().BeNullOrEmpty();
        episode.Urls!.Spotify.Should().BeNull();
        episode.Urls.Apple.Should().BeNull();
        episode.Urls.YouTube.Should().BeNull();
        episode.Images.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an Apple candidate link id is not a parseable long, materialization keeps the Apple URL but leaves AppleId unset " +
        "because legacy parsing is tolerant.")]
    public void Apple_non_numeric_link_id_keeps_url_without_apple_id()
    {
        // Arrange
        var input = _fixture.CreateAppleCatalogueInput(b => b.WithImage(null));
        var candidate = new EpisodeCandidate(
            input.Title,
            input.Description,
            input.Duration,
            new ReleaseInfo(input.Release, ReleasePrecision.DateTimeUtc),
            new PlatformLink(Service.Apple, "not-a-numeric-id", input.AppleUrl, null));

        // Act
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.AppleId.Should().BeNull();
        episode.Urls!.Apple.Should().Be(input.AppleUrl);
    }

    private EpisodeCandidate CreateCandidate(CataloguePlatform platform, bool includeArtwork)
    {
        Uri? artwork = includeArtwork ? _fixture.Create<Uri>() : null;

        return platform switch
        {
            CataloguePlatform.Spotify => new SpotifyEpisodeAdapter().Adapt(
                _fixture.CreateSpotifyCatalogueInput(b => b.WithImage(artwork))),
            CataloguePlatform.Apple => new AppleEpisodeAdapter().Adapt(
                _fixture.CreateAppleCatalogueInput(b => b.WithImage(artwork))),
            CataloguePlatform.YouTube => new YouTubeEpisodeAdapter().Adapt(
                _fixture.CreateYouTubeCatalogueInput(b => b.WithImage(artwork))),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
    }

    public enum CataloguePlatform
    {
        Spotify,
        Apple,
        YouTube
    }
}
