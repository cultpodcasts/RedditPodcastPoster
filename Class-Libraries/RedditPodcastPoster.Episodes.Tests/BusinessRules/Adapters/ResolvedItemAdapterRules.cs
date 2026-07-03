using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
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
        var input = _fixture.CreateResolvedSpotifyItemInput();

        // Act
        var candidate = _spotifyAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.Spotify, input.EpisodeId, input.Url, input.Image));

        var expected = new EpisodeExpectation(
            new PlatformExpectation(input.EpisodeId, input.Url, input.Image),
            null,
            null,
            input.Release,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateOnly);
        candidate.Release.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "When a ResolvedAppleItem is adapted, the candidate SourceLink is an Apple PlatformLink " +
        "with episode id, URL, and artwork from the resolved item.")]
    public void ResolvedAppleItem_maps_to_Apple_PlatformLink()
    {
        // Arrange
        var input = _fixture.CreateResolvedAppleItemInput();

        // Act
        var candidate = _appleAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.Apple, input.EpisodeId.ToString(), input.Url, input.Image));

        var expected = new EpisodeExpectation(
            null,
            new PlatformExpectation(input.EpisodeId.ToString(), input.Url, input.Image),
            null,
            input.Release,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        input.Release.TimeOfDay.Should().Be(DomainTestFixture.Specimens.DefaultAppleReleaseTimeOfDay);
    }

    [Fact(DisplayName =
        "When a ResolvedYouTubeItem is adapted, the candidate SourceLink is a YouTube PlatformLink " +
        "with video id, URL, and artwork from the resolved item.")]
    public void ResolvedYouTubeItem_maps_to_YouTube_PlatformLink()
    {
        // Arrange
        var input = _fixture.CreateResolvedYouTubeItemInput();

        // Act
        var candidate = _youTubeAdapter.Adapt(input);

        // Assert
        candidate.SourceLink.Should().BeEquivalentTo(
            new PlatformLink(Service.YouTube, input.EpisodeId, input.Url, input.Image));

        var expected = new EpisodeExpectation(
            null,
            null,
            new PlatformExpectation(input.EpisodeId, input.Url, input.Image),
            input.Release,
            input.EpisodeDescription);
        EpisodeExpectation.From(candidate).Should().BeEquivalentTo(expected);
        candidate.Release.Precision.Should().Be(ReleasePrecision.DateTimeUtc);
        input.Release.TimeOfDay.Should().Be(DomainTestFixture.Specimens.DefaultYouTubeReleaseTimeOfDay);
    }
}
