using FluentAssertions;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Stored episode to candidate mapping at domain boundaries.
/// </summary>
public class EpisodeMappingExtensionsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When mapping a stored episode with Spotify identity to a candidate, " +
        "the candidate carries Spotify link, duration, and date-only release semantics.")]
    public void stored_spotify_episode_maps_to_candidate_with_platform_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);

        // Act
        var candidate = stored.ToCandidate(Service.Spotify);

        // Assert
        candidate.Title.Should().Be(stored.Title);
        candidate.Duration.Should().Be(stored.Length);
        candidate.SourceLink.Should().NotBeNull();
        candidate.SourceLink!.Service.Should().Be(Service.Spotify);
        candidate.SourceLink.Id.Should().Be(stored.SpotifyId);
        candidate.SourceLink.Url.Should().Be(stored.Urls.Spotify);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(stored.Release);
    }

    [Fact(DisplayName =
        "When mapping a stored YouTube-only episode to a candidate, " +
        "the candidate preserves YouTube link and full datetime release.")]
    public void stored_youtube_episode_maps_to_candidate_with_youtube_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var candidate = stored.ToCandidate(Service.YouTube);

        // Assert
        candidate.SourceLink.Should().NotBeNull();
        candidate.SourceLink!.Service.Should().Be(Service.YouTube);
        candidate.SourceLink.Id.Should().Be(stored.YouTubeId);
        candidate.SourceLink.Url.Should().Be(stored.Urls.YouTube);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        stored.Release.TimeOfDay.Should().NotBe(TimeSpan.Zero);
        candidate.Release.Value.Should().Be(stored.Release);
    }

    [Fact(DisplayName =
        "When building a platform patch from a stored episode, " +
        "ToSpotifyPatch carries Spotify link without mutating other platform fields.")]
    public void to_spotify_patch_carries_spotify_link_only()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);

        // Act
        var patch = stored.ToSpotifyPatch();

        // Assert
        patch.Link.Should().NotBeNull();
        patch.Link!.Service.Should().Be(Service.Spotify);
        patch.Link.Id.Should().Be(stored.SpotifyId);
        patch.Description.Should().Be(stored.Description);
        patch.Release.Should().NotBeNull();
        patch.Release!.Value.Should().Be(stored.Release);
    }

    [Fact(DisplayName =
        "When mapping a stored episode with Apple identity to a candidate, " +
        "the candidate carries Apple link and numeric platform id.")]
    public void stored_apple_episode_maps_to_candidate_with_platform_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.AppleId = _fixture.CreateAppleId();
            e.Urls = new ServiceUrls
            {
                Apple = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{e.AppleId}")
            };
        });

        // Act
        var candidate = stored.ToCandidate(Service.Apple);

        // Assert
        candidate.SourceLink.Should().NotBeNull();
        candidate.SourceLink!.Service.Should().Be(Service.Apple);
        candidate.SourceLink.Id.Should().Be(stored.AppleId!.Value.ToString());
        candidate.SourceLink.Url.Should().Be(stored.Urls.Apple);
    }

    [Fact(DisplayName =
        "When building Apple and YouTube platform patches from a stored episode, " +
        "each patch carries only that platform's link and release.")]
    public void apple_and_youtube_patches_carry_respective_platform_links()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeId());
        stored.AppleId = _fixture.CreateAppleId();
        stored.Urls.Apple = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{stored.AppleId}");

        // Act
        var applePatch = stored.ToApplePatch();
        var youTubePatch = stored.ToYouTubePatch();

        // Assert
        applePatch.Link!.Service.Should().Be(Service.Apple);
        applePatch.Link.Id.Should().Be(stored.AppleId!.Value.ToString());
        youTubePatch.Link!.Service.Should().Be(Service.YouTube);
        youTubePatch.Link.Id.Should().Be(stored.YouTubeId);
    }

    [Fact(DisplayName =
        "When a stored episode has no platform identity for the requested service, " +
        "ToCandidate returns a candidate with a null source link.")]
    public void to_candidate_without_platform_identity_has_null_source_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.SpotifyId = string.Empty;
            e.Urls = new ServiceUrls();
        });

        // Act
        var candidate = stored.ToCandidate(Service.Spotify);

        // Assert
        candidate.SourceLink.Should().BeNull();
        candidate.Title.Should().Be(stored.Title);
    }

    [Fact(DisplayName =
        "When building a generic platform patch from a stored episode, " +
        "ToPlatformPatch carries description and release but no platform link.")]
    public void to_platform_patch_carries_description_without_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);

        // Act
        var patch = stored.ToPlatformPatch();

        // Assert
        patch.Link.Should().BeNull();
        patch.Description.Should().Be(stored.Description);
        patch.Release!.Value.Should().Be(stored.Release);
    }

    public static TheoryData<Service> AllPlatformServices() =>
        new()
        {
            Service.Spotify,
            Service.Apple,
            Service.YouTube
        };

    [Theory(DisplayName =
        "ToCandidate maps each platform service to a SourceLink with that platform's id and URL.")]
    [MemberData(nameof(AllPlatformServices))]
    public void to_candidate_maps_each_platform_service(Service service)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = CreateEpisodeWithAllPlatformLinks(podcast);

        // Act
        var candidate = stored.ToCandidate(service);

        // Assert
        candidate.SourceLink.Should().NotBeNull();
        candidate.SourceLink!.Service.Should().Be(service);
        candidate.SourceLink.Id.Should().Be(service switch
        {
            Service.Spotify => stored.SpotifyId,
            Service.Apple => stored.AppleId!.Value.ToString(),
            Service.YouTube => stored.YouTubeId,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        });
        candidate.SourceLink.Url.Should().Be(service switch
        {
            Service.Spotify => stored.Urls.Spotify,
            Service.Apple => stored.Urls.Apple,
            Service.YouTube => stored.Urls.YouTube,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        });
    }

    [Theory(DisplayName =
        "Platform-specific To*Patch helpers carry only the requested platform link.")]
    [MemberData(nameof(AllPlatformServices))]
    public void platform_patch_helpers_carry_requested_platform_link(Service service)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = CreateEpisodeWithAllPlatformLinks(podcast);

        // Act
        var patch = service switch
        {
            Service.Spotify => stored.ToSpotifyPatch(),
            Service.Apple => stored.ToApplePatch(),
            Service.YouTube => stored.ToYouTubePatch(),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };

        // Assert
        patch.Link.Should().NotBeNull();
        patch.Link!.Service.Should().Be(service);
        patch.Description.Should().Be(stored.Description);
        patch.Release!.Value.Should().Be(stored.Release);
    }

    [Fact(DisplayName =
        "ToCandidate with an unsupported source service returns a candidate with a null source link.")]
    public void to_candidate_unsupported_service_has_null_source_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = CreateEpisodeWithAllPlatformLinks(podcast);

        // Act
        var candidate = stored.ToCandidate(Service.Other);

        // Assert
        candidate.SourceLink.Should().BeNull();
        candidate.Title.Should().Be(stored.Title);
    }

    private Episode CreateEpisodeWithAllPlatformLinks(Podcast podcast)
    {
        var stored = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeId());
        stored.AppleId = _fixture.CreateAppleId();
        stored.Urls.Apple = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{stored.AppleId}");
        return stored;
    }
}
