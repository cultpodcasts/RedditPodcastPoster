using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// F17 boundary: platform resolved types map to UrlSubmission-owned DTOs at categoriser boundary only;
/// enrichment uses <see cref="CategorisedSpotifyItem.ToAdapterInput"/> (and Apple/YouTube equivalents).
/// </summary>
public class UrlSubmissionCategorisationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly ResolvedSpotifyItemAdapter _spotifyAdapter = new();
    private readonly ResolvedAppleItemAdapter _appleAdapter = new();
    private readonly ResolvedYouTubeItemAdapter _youTubeAdapter = new();

    public static TheoryData<Service> AllCategorisedPlatforms() =>
        new()
        {
            Service.Spotify,
            Service.Apple,
            Service.YouTube
        };

    [Fact(DisplayName =
        "When a platform ResolvedSpotifyItem is mapped at the categoriser boundary, " +
        "every DTO field on CategorisedSpotifyItem mirrors the platform resolved item.")]
    public void from_platform_maps_all_spotify_dto_fields()
    {
        // Arrange
        var platformItem = CreateSpotifyPlatformItem();

        // Act
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);

        // Assert
        dto.ShowId.Should().Be(platformItem.ShowId);
        dto.EpisodeId.Should().Be(platformItem.EpisodeId);
        dto.ShowName.Should().Be(platformItem.ShowName);
        dto.ShowDescription.Should().Be(platformItem.ShowDescription);
        dto.Publisher.Should().Be(platformItem.Publisher);
        dto.EpisodeTitle.Should().Be(platformItem.EpisodeTitle);
        dto.EpisodeDescription.Should().Be(platformItem.EpisodeDescription);
        dto.Release.Should().Be(platformItem.Release);
        dto.Duration.Should().Be(platformItem.Duration);
        dto.Url.Should().Be(platformItem.Url);
        dto.Explicit.Should().Be(platformItem.Explicit);
        dto.Image.Should().Be(platformItem.Image);
    }

    [Fact(DisplayName =
        "When a platform ResolvedAppleItem is mapped at the categoriser boundary, " +
        "every DTO field on CategorisedAppleItem mirrors the platform resolved item.")]
    public void from_platform_maps_all_apple_dto_fields()
    {
        // Arrange
        var platformItem = CreateApplePlatformItem();

        // Act
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);

        // Assert
        dto.ShowId.Should().Be(platformItem.ShowId);
        dto.EpisodeId.Should().Be(platformItem.EpisodeId);
        dto.ShowName.Should().Be(platformItem.ShowName);
        dto.ShowDescription.Should().Be(platformItem.ShowDescription);
        dto.Publisher.Should().Be(platformItem.Publisher);
        dto.EpisodeTitle.Should().Be(platformItem.EpisodeTitle);
        dto.EpisodeDescription.Should().Be(platformItem.EpisodeDescription);
        dto.Release.Should().Be(platformItem.Release);
        dto.Duration.Should().Be(platformItem.Duration);
        dto.Url.Should().Be(platformItem.Url);
        dto.Explicit.Should().Be(platformItem.Explicit);
        dto.Image.Should().Be(platformItem.Image);
    }

    [Fact(DisplayName =
        "When a platform ResolvedYouTubeItem is mapped at the categoriser boundary, " +
        "every DTO field on CategorisedYouTubeItem mirrors the platform resolved item including PlaylistId.")]
    public void from_platform_maps_all_youtube_dto_fields_including_playlist_id()
    {
        // Arrange
        var platformItem = CreateYouTubePlatformItem();

        // Act
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);

        // Assert
        dto.ShowId.Should().Be(platformItem.ShowId);
        dto.EpisodeId.Should().Be(platformItem.EpisodeId);
        dto.ShowName.Should().Be(platformItem.ShowName);
        dto.ShowDescription.Should().Be(platformItem.ShowDescription);
        dto.Publisher.Should().Be(platformItem.Publisher);
        dto.EpisodeTitle.Should().Be(platformItem.EpisodeTitle);
        dto.EpisodeDescription.Should().Be(platformItem.EpisodeDescription);
        dto.Release.Should().Be(platformItem.Release);
        dto.Duration.Should().Be(platformItem.Duration);
        dto.Url.Should().Be(platformItem.Url);
        dto.Explicit.Should().Be(platformItem.Explicit);
        dto.Image.Should().Be(platformItem.Image);
        dto.PlaylistId.Should().Be(platformItem.PlaylistId);
    }

    [Theory(DisplayName =
        "When a categorised platform DTO is converted for enrichment, ToAdapterInput carries " +
        "episode id, title, description, release, duration, URL, and artwork.")]
    [MemberData(nameof(AllCategorisedPlatforms))]
    public void to_adapter_input_carries_enrichment_fields(Service platform)
    {
        // Arrange
        var (dto, expectedInput) = CreateDtoAndDirectInput(platform);

        // Act
        var adapterInput = ToAdapterInput(dto, platform);

        // Assert
        adapterInput.Should().BeEquivalentTo(expectedInput);
    }

    [Theory(DisplayName =
        "When enrichment adapts a categorised DTO via ToAdapterInput, the EpisodeCandidate matches " +
        "adapting the equivalent Resolved*ItemInput directly.")]
    [MemberData(nameof(AllCategorisedPlatforms))]
    public void dto_to_adapter_input_produces_same_candidate_as_direct_resolved_input(Service platform)
    {
        // Arrange
        var (dto, directInput) = CreateDtoAndDirectInput(platform);

        // Act
        var fromDto = Adapt(dto, platform);
        var fromDirect = Adapt(directInput, platform);

        // Assert
        EpisodeExpectation.From(fromDto).Should().BeEquivalentTo(EpisodeExpectation.From(fromDirect));
    }

    private (object Dto, object DirectInput) CreateDtoAndDirectInput(Service platform) =>
        platform switch
        {
            Service.Spotify => CreateSpotifyDtoPair(),
            Service.Apple => CreateAppleDtoPair(),
            Service.YouTube => CreateYouTubeDtoPair(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

    private (CategorisedSpotifyItem Dto, object DirectInput) CreateSpotifyDtoPair()
    {
        var platformItem = CreateSpotifyPlatformItem();
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);
        var directInput = _fixture.CreateResolvedSpotifyItemInput(b => b
            .WithEpisodeId(platformItem.EpisodeId)
            .WithTitle(platformItem.EpisodeTitle)
            .WithDescription(platformItem.EpisodeDescription)
            .WithRelease(platformItem.Release)
            .WithDuration(platformItem.Duration)
            .WithUrl(platformItem.Url!)
            .WithImage(platformItem.Image));
        return (dto, directInput);
    }

    private (CategorisedAppleItem Dto, object DirectInput) CreateAppleDtoPair()
    {
        var platformItem = CreateApplePlatformItem();
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);
        var directInput = _fixture.CreateResolvedAppleItemInput(b => b
            .WithEpisodeId(platformItem.EpisodeId!.Value)
            .WithTitle(platformItem.EpisodeTitle)
            .WithDescription(platformItem.EpisodeDescription)
            .WithRelease(platformItem.Release)
            .WithDuration(platformItem.Duration)
            .WithUrl(platformItem.Url!)
            .WithImage(platformItem.Image));
        return (dto, directInput);
    }

    private (CategorisedYouTubeItem Dto, object DirectInput) CreateYouTubeDtoPair()
    {
        var platformItem = CreateYouTubePlatformItem();
        var dto = PlatformResolvedItemMappers.FromPlatform(platformItem);
        var directInput = _fixture.CreateResolvedYouTubeItemInput(b => b
            .WithEpisodeId(platformItem.EpisodeId)
            .WithTitle(platformItem.EpisodeTitle)
            .WithDescription(platformItem.EpisodeDescription)
            .WithRelease(platformItem.Release)
            .WithDuration(platformItem.Duration)
            .WithUrl(platformItem.Url!)
            .WithImage(platformItem.Image));
        return (dto, directInput);
    }

    private static object ToAdapterInput(object dto, Service platform) =>
        platform switch
        {
            Service.Spotify => ((CategorisedSpotifyItem)dto).ToAdapterInput(),
            Service.Apple => ((CategorisedAppleItem)dto).ToAdapterInput(),
            Service.YouTube => ((CategorisedYouTubeItem)dto).ToAdapterInput(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

    private EpisodeCandidate Adapt(object input, Service platform) =>
        platform switch
        {
            Service.Spotify when input is CategorisedSpotifyItem spotifyDto =>
                _spotifyAdapter.Adapt(spotifyDto.ToAdapterInput()),
            Service.Spotify => _spotifyAdapter.Adapt((RedditPodcastPoster.Episodes.Adapters.Inputs.ResolvedSpotifyItemInput)input),
            Service.Apple when input is CategorisedAppleItem appleDto =>
                _appleAdapter.Adapt(appleDto.ToAdapterInput()),
            Service.Apple => _appleAdapter.Adapt((RedditPodcastPoster.Episodes.Adapters.Inputs.ResolvedAppleItemInput)input),
            Service.YouTube when input is CategorisedYouTubeItem youTubeDto =>
                _youTubeAdapter.Adapt(youTubeDto.ToAdapterInput()),
            Service.YouTube => _youTubeAdapter.Adapt((RedditPodcastPoster.Episodes.Adapters.Inputs.ResolvedYouTubeItemInput)input),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

    private ResolvedSpotifyItem CreateSpotifyPlatformItem()
    {
        var showId = _fixture.CreateSpotifyId();
        var episodeId = _fixture.CreateSpotifyId();
        var url = _fixture.DefaultSpotifyUrl(episodeId);
        return new ResolvedSpotifyItem(
            showId,
            episodeId,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcDateDaysAgo(5),
            _fixture.CreateDuration(),
            url,
            false,
            new Uri($"https://example.test/spotify-art/{episodeId}"));
    }

    private ResolvedAppleItem CreateApplePlatformItem()
    {
        var showId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateAppleId();
        var url = _fixture.DefaultAppleUrl(episodeId);
        return new ResolvedAppleItem(
            showId,
            episodeId,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(),
            url,
            false,
            new Uri($"https://example.test/apple-art/{episodeId}"));
    }

    private ResolvedYouTubeItem CreateYouTubePlatformItem()
    {
        var showId = _fixture.CreateYouTubeChannelId();
        var episodeId = _fixture.CreateYouTubeId();
        var url = _fixture.DefaultYouTubeUrl(episodeId);
        return new ResolvedYouTubeItem(
            showId,
            episodeId,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(),
            url,
            false,
            new Uri($"https://example.test/youtube-art/{episodeId}"),
            _fixture.Create<string>());
    }
}
