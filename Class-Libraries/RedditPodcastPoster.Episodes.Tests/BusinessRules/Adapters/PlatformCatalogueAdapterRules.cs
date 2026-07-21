using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Adapters;

/// <summary>
/// Layer 1 adapter rules — platform catalogue payloads map to domain candidates at boundaries.
/// </summary>
public class PlatformCatalogueAdapterRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly SpotifyEpisodeAdapter _spotifyAdapter = new();
    private readonly AppleEpisodeAdapter _appleAdapter = new();
    private readonly YouTubeEpisodeAdapter _youTubeAdapter = new();

    [Fact(DisplayName =
        "When a Spotify catalogue episode is adapted, release is mapped with DateOnly precision " +
        "because Spotify catalogue dates have no time-of-day.")]
    public void Spotify_catalogue_release_maps_to_date_only_precision()
    {
        // Arrange — Spotify specimens are date-only at source; adapter preserves midnight UTC.
        var input = _fixture.CreateSpotifyCatalogueInput();

        // Act
        var candidate = _spotifyAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateOnly);
        candidate.Release.Value.Should().Be(input.Release);
        candidate.Release.Value.TimeOfDay.Should().Be(TimeSpan.Zero);

        var expected = new EpisodeExpectation(
            new PlatformExpectation(input.SpotifyId, input.SpotifyUrl, input.Image),
            null,
            null,
            input.Release,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When an Apple catalogue episode is adapted, release is mapped with DateTimeUtc precision " +
        "because Apple provides a full publish datetime.")]
    public void Apple_catalogue_release_maps_to_datetime_utc_precision()
    {
        // Arrange — Apple specimens carry a full UTC publish datetime from fixture setup.
        var input = _fixture.CreateAppleCatalogueInput();

        // Act
        var candidate = _appleAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(input.Release);
        input.Release.TimeOfDay.Should().NotBe(TimeSpan.Zero);

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(input.AppleId.ToString(), input.AppleUrl, input.Image),
            null,
            input.Release,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When a YouTube catalogue episode is adapted, publish datetime is mapped as-is " +
        "because YouTube authority depends on the exact publish time.")]
    public void YouTube_catalogue_release_maps_publish_datetime_as_is()
    {
        // Arrange — YouTube specimens carry a full UTC publish datetime from fixture setup.
        var input = _fixture.CreateYouTubeCatalogueInput();

        // Act
        var candidate = _youTubeAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(input.Release);
        input.Release.TimeOfDay.Should().NotBe(TimeSpan.Zero);

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(input.YouTubeId, input.YouTubeUrl, input.Image),
            input.Release,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When PlatformLinkFactory receives null id, null url, and null image, it returns null " +
        "so adapters do not materialize empty platform links.")]
    public void platform_link_factory_returns_null_when_all_inputs_are_null()
    {
        // Arrange & Act
        var link = PlatformLinkFactory.Create(Service.Spotify, id: null, url: null, image: null);

        // Assert
        link.Should().BeNull();
    }

    public static TheoryData<string> PartialPlatformLinkInputs =>
        new()
        {
            "id_only",
            "url_only",
            "image_only",
            "all_present"
        };

    [Theory(DisplayName =
        "PlatformLinkFactory materializes a link when any of id, url, or image is present, " +
        "and normalizes blank ids to null.")]
    [MemberData(nameof(PartialPlatformLinkInputs))]
    public void platform_link_factory_partial_input_scenarios(string scenario)
    {
        // Arrange
        var id = scenario is "id_only" or "all_present" ? _fixture.CreateSpotifyId() : null;
        var url = scenario is "url_only" or "all_present"
            ? new Uri("https://open.spotify.com/episode/partialLink")
            : null;
        var image = scenario is "image_only" or "all_present"
            ? _fixture.Create<Uri>()
            : null;

        // Act
        var link = PlatformLinkFactory.Create(Service.Spotify, id, url, image);

        // Assert
        link.Should().NotBeNull();
        link!.Service.Should().Be(Service.Spotify);
        link.Id.Should().Be(id);
        link.Url.Should().Be(url);
        link.Image.Should().Be(image);
    }

    [Fact(DisplayName =
        "PlatformLinkFactory treats whitespace-only ids as absent and returns null when url and image are also null.")]
    public void platform_link_factory_whitespace_id_alone_returns_null()
    {
        // Act
        var link = PlatformLinkFactory.Create(Service.Apple, id: "   ", url: null, image: null);

        // Assert
        link.Should().BeNull();
    }
}
