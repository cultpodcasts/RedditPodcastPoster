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
        // Given Spotify catalogue fields matching Episode.FromSpotify inputs
        const string spotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{spotifyId}");
        var catalogueRelease = new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc);
        var input = new SpotifyCatalogueInput(
            spotifyId,
            "Spotify episode title",
            "Spotify description",
            TimeSpan.FromMinutes(52),
            catalogueRelease,
            spotifyUrl,
            new Uri("https://i.scdn.co/image/spotify-art"));

        // When the Spotify adapter maps to an EpisodeCandidate
        var candidate = _spotifyAdapter.Adapt(input);

        // Then release is date-only at midnight UTC and the Spotify platform link is preserved
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateOnly);
        candidate.Release.Value.Should().Be(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Unspecified));

        var expected = new EpisodeExpectation(
            new PlatformExpectation(spotifyId, spotifyUrl, input.Image),
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
        // Given Apple catalogue fields matching Episode.FromApple inputs
        const long appleId = 1234567890;
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var appleRelease = new DateTime(2026, 4, 10, 9, 15, 30, DateTimeKind.Utc);
        var input = new AppleCatalogueInput(
            appleId,
            "Apple episode title",
            "Apple description",
            TimeSpan.FromMinutes(38),
            appleRelease,
            appleUrl,
            new Uri("https://example.com/apple-art.jpg"));

        // When the Apple adapter maps to an EpisodeCandidate
        var candidate = _appleAdapter.Adapt(input);

        // Then release keeps the full UTC datetime and the Apple platform link is preserved
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(appleRelease);

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(appleId.ToString(), appleUrl, input.Image),
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
        // Given YouTube catalogue fields matching Episode.FromYouTube inputs
        const string youTubeId = "dQw4w9WgXcQ";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var publishDate = new DateTime(2026, 5, 20, 18, 45, 12, DateTimeKind.Utc);
        var input = new YouTubeCatalogueInput(
            youTubeId,
            "YouTube episode title",
            "YouTube description",
            TimeSpan.FromMinutes(61),
            publishDate,
            youTubeUrl,
            new Uri("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg"));

        // When the YouTube adapter maps to an EpisodeCandidate
        var candidate = _youTubeAdapter.Adapt(input);

        // Then publish datetime is unchanged and the YouTube platform link is preserved
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        candidate.Release.Value.Should().Be(publishDate);

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(youTubeId, youTubeUrl, input.Image),
            publishDate,
            input.Description);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
    }
}
