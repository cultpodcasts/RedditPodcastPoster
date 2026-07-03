using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Adapters;

/// <summary>
/// Layer 1 adapter rules — UrlSubmission Resolved*Item payloads map to domain platform links.
/// </summary>
public class ResolvedItemAdapterRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly ResolvedSpotifyItemAdapter _spotifyAdapter = new();
    private readonly ResolvedAppleItemAdapter _appleAdapter = new();
    private readonly ResolvedYouTubeItemAdapter _youTubeAdapter = new();

    [Fact(DisplayName =
        "When a ResolvedSpotifyItem is adapted, the candidate SourceLink is a Spotify PlatformLink " +
        "with episode id, URL, and artwork from the resolved item.")]
    public void ResolvedSpotifyItem_maps_to_Spotify_PlatformLink()
    {
        // Arrange
        const string episodeId = "submit-spot-1";
        var url = new Uri($"https://open.spotify.com/episode/{episodeId}");
        var image = new Uri("https://i.scdn.co/image/resolved-spotify");
        var input = new ResolvedSpotifyItemInput(
            episodeId,
            "Resolved Spotify title",
            "Resolved Spotify description",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(44),
            url,
            image);

        // Act
        var candidate = _spotifyAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.Spotify, episodeId, url, image));

        var expected = new EpisodeExpectation(
            new PlatformExpectation(episodeId, url, image),
            null,
            null,
            input.Release.Date,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateOnly);
    }

    [Fact(DisplayName =
        "When a ResolvedAppleItem is adapted, the candidate SourceLink is an Apple PlatformLink " +
        "with episode id, URL, and artwork from the resolved item.")]
    public void ResolvedAppleItem_maps_to_Apple_PlatformLink()
    {
        // Arrange
        const long episodeId = 1112223334;
        var url = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{episodeId}");
        var image = new Uri("https://example.com/resolved-apple.jpg");
        var release = new DateTime(2026, 6, 2, 11, 30, 0, DateTimeKind.Utc);
        var input = new ResolvedAppleItemInput(
            episodeId,
            "Resolved Apple title",
            "Resolved Apple description",
            release,
            TimeSpan.FromMinutes(50),
            url,
            image);

        // Act
        var candidate = _appleAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.Apple, episodeId.ToString(), url, image));

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(episodeId.ToString(), url, image),
            null,
            release,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
    }

    [Fact(DisplayName =
        "When a ResolvedYouTubeItem is adapted, the candidate SourceLink is a YouTube PlatformLink " +
        "with video id, URL, and artwork from the resolved item.")]
    public void ResolvedYouTubeItem_maps_to_YouTube_PlatformLink()
    {
        // Arrange
        const string episodeId = "yt-only-submit";
        var url = new Uri($"https://www.youtube.com/watch?v={episodeId}");
        var image = new Uri("https://i.ytimg.com/vi/yt-only-submit/hqdefault.jpg");
        var release = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var input = new ResolvedYouTubeItemInput(
            episodeId,
            "Resolved YouTube title",
            "Resolved YouTube description",
            release,
            TimeSpan.FromMinutes(45),
            url,
            image);

        // Act
        var candidate = _youTubeAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.YouTube, episodeId, url, image));

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(episodeId, url, image),
            release,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
    }
}
