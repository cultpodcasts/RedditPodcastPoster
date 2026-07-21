using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Direct characterization of <see cref="SpotifyCatalogueReleaseMatchStrategy.Evaluate"/>.
/// </summary>
public class SpotifyCatalogueReleaseMatchStrategyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly SpotifyCatalogueReleaseMatchStrategy _strategy = new();

    [Fact(DisplayName =
        "When the podcast has no YouTube publishing delay, Spotify catalogue release strategy " +
        "defers to other strategies by returning null.")]
    public void zero_delay_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored and incoming episodes share the same platform type, Spotify catalogue release strategy " +
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
        "When release authority is not YouTube, Spotify catalogue release strategy defers " +
        "even for cross-platform YouTube-stored and Spotify-incoming pairs.")]
    public void non_youtube_release_authority_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(-31).Ticks;
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored episode is not YouTube-identified, Spotify catalogue release strategy defers " +
        "for Spotify-incoming pairs.")]
    public void stored_without_youtube_identity_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var stored = _fixture.CreateSpotifyCatalogueEpisode();
        var incoming = _fixture.CreateSpotifyCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "For YouTube release authority with negative publishing delay, a stored YouTube episode " +
        "matches an aligned incoming Spotify catalogue item after delay adjustment.")]
    public void youtube_stored_spotify_incoming_aligned_returns_true()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, incoming, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For YouTube release authority with negative publishing delay, a stored YouTube episode " +
        "does not match when the incoming Spotify catalogue release falls outside tolerance.")]
    public void youtube_stored_spotify_incoming_outside_tolerance_returns_false()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            release: youTubeRelease);
        // Before YouTube day — outside ±5 of expected and outside early-within-delay window.
        var farOffSpotifyRelease = youTubeRelease.AddDays(-5);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(farOffSpotifyRelease));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "For YouTube release authority with positive publishing delay, a stored YouTube episode " +
        "matches an aligned incoming Spotify catalogue item after delay adjustment.")]
    public void positive_delay_youtube_authority_aligned_spotify_returns_true()
    {
        // Arrange
        var delay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            delay.Ticks);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: youTubeRelease);
        var expectedAudioRelease = youTubeRelease - delay;
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(expectedAudioRelease));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When an incoming Apple episode is paired with a stored YouTube episode, " +
        "Spotify catalogue release strategy defers because Apple is not a Spotify catalogue lookup.")]
    public void youtube_stored_apple_incoming_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateAppleCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored length is zero, Spotify catalogue release strategy uses incoming length " +
        "as the tolerance reference for an aligned YouTube-to-Spotify pair.")]
    public void zero_stored_length_uses_incoming_length_for_aligned_match()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, incoming, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        stored.Length = TimeSpan.Zero;
        incoming.Length = _fixture.CreateDuration();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }
}
