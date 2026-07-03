using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
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
        // Arrange
        var appleRelease = DomainTestFixture.UtcAtTime(-84, TimeSpan.FromHours(9) + TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(30));
        var input = _fixture.CreateAppleCatalogueInput(b => b.WithRelease(appleRelease));

        // Act
        var candidate = _appleAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(appleRelease);

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(input.AppleId.ToString(), input.AppleUrl, input.Image),
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
        var publishDate = DomainTestFixture.UtcAtTime(-44, TimeSpan.FromHours(18) + TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(12));
        var input = _fixture.CreateYouTubeCatalogueInput(b => b.WithRelease(publishDate));

        // Act
        var candidate = _youTubeAdapter.Adapt(input);

        // Assert
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(publishDate);

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(input.YouTubeId, input.YouTubeUrl, input.Image),
            publishDate,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }
}
