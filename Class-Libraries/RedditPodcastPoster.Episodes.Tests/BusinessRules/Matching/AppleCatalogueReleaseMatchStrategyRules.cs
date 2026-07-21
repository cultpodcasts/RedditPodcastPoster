using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Direct characterization of <see cref="AppleCatalogueReleaseMatchStrategy.Evaluate"/>.
/// </summary>
public class AppleCatalogueReleaseMatchStrategyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AppleCatalogueReleaseMatchStrategy _strategy = new();

    [Fact(DisplayName =
        "When the podcast has no YouTube publishing delay, Apple catalogue release strategy " +
        "defers to other strategies by returning null.")]
    public void zero_delay_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateAppleCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored and incoming episodes share the same platform type, Apple catalogue release strategy " +
        "defers by returning null.")]
    public void same_platform_type_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateYouTubeCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When release authority is not YouTube, Apple catalogue release strategy defers " +
        "even for cross-platform YouTube-stored and Apple-incoming pairs.")]
    public void non_youtube_release_authority_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(-31).Ticks;
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateAppleCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored episode is not YouTube-identified, Apple catalogue release strategy defers " +
        "for Apple-incoming pairs.")]
    public void stored_without_youtube_identity_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var stored = _fixture.CreateAppleCatalogueEpisode();
        var incoming = _fixture.CreateAppleCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "For YouTube release authority with negative publishing delay, a stored YouTube episode " +
        "matches an aligned incoming Apple catalogue item after delay adjustment.")]
    public void youtube_stored_apple_incoming_aligned_returns_true()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, incoming, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityApplePair(podcast);
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For YouTube release authority with negative publishing delay, a stored YouTube episode " +
        "does not match when the incoming Apple catalogue release falls outside tolerance.")]
    public void youtube_stored_apple_incoming_outside_tolerance_returns_false()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            release: youTubeRelease);
        // Before YouTube day — outside ±5 of expected and outside early-within-delay window.
        var farOffAppleRelease = youTubeRelease.AddDays(-5);
        var incoming = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithRelease(farOffAppleRelease));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an incoming Spotify episode is paired with a stored YouTube episode, " +
        "Apple catalogue release strategy defers because Spotify is not an Apple catalogue lookup.")]
    public void youtube_stored_spotify_incoming_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }
}
