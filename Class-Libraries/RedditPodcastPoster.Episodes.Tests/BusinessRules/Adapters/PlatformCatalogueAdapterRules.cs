using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

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
        // Arrange
        const string spotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";
        var catalogueRelease = DomainTestFixture.UtcAtTime(-110, TimeSpan.FromHours(14) + TimeSpan.FromMinutes(30));
        var input = _fixture.CreateSpotifyCatalogueInput(spotifyId, release: catalogueRelease);

        // Act
        var candidate = _spotifyAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateOnly);
        candidate.Release.Value.Should().Be(
            new DateTime(catalogueRelease.Year, catalogueRelease.Month, catalogueRelease.Day, 0, 0, 0, DateTimeKind.Unspecified));

        var expected = new EpisodeExpectation(
            new PlatformExpectation(spotifyId, input.SpotifyUrl, input.Image),
            null,
            null,
            candidate.Release.Value,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When an Apple catalogue episode is adapted, release is mapped with DateTimeUtc precision " +
        "because Apple provides a full publish datetime.")]
    public void Apple_catalogue_release_maps_to_datetime_utc_precision()
    {
        // Arrange
        const long appleId = 1234567890;
        var appleRelease = DomainTestFixture.UtcAtTime(-84, TimeSpan.FromHours(9) + TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(30));
        var input = _fixture.CreateAppleCatalogueInput(appleId, release: appleRelease);

        // Act
        var candidate = _appleAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(appleRelease);

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(appleId.ToString(), input.AppleUrl, input.Image),
            null,
            appleRelease,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When a YouTube catalogue episode is adapted, publish datetime is mapped as-is " +
        "because YouTube authority depends on the exact publish time.")]
    public void YouTube_catalogue_release_maps_publish_datetime_as_is()
    {
        // Arrange
        const string youTubeId = "dQw4w9WgXcQ";
        var publishDate = DomainTestFixture.UtcAtTime(-44, TimeSpan.FromHours(18) + TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(12));
        var input = _fixture.CreateYouTubeCatalogueInput(youTubeId, release: publishDate);

        // Act
        var candidate = _youTubeAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(publishDate);

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(youTubeId, input.YouTubeUrl, input.Image),
            publishDate,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }
}
