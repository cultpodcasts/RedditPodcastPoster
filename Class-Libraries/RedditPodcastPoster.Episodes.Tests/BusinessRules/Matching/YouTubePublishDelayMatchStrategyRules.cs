using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Release-match strategy rules for positive YouTube publishing delay alignment.
/// </summary>
public class YouTubePublishDelayMatchStrategyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly YouTubePublishDelayMatchStrategy _strategy = new();

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode " +
        "matches a stored audio episode when its release aligns after delay adjustment.")]
    public void positive_delay_aligned_youtube_release_matches_stored_audio_episode()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreatePositiveDelayAudioStoredEpisode(
            podcast,
            audioRelease: audioRelease);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(audioRelease.Add(publishingDelay)));
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithRelease(youTubeInput.Release));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode " +
        "defers when its release exceeds the delay-alignment threshold — offset is a confidence signal, not a hard veto.")]
    public void positive_delay_release_outside_threshold_defers()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreatePositiveDelayAudioStoredEpisode(
            podcast,
            audioRelease: audioRelease);
        var alignedRelease = audioRelease.Add(publishingDelay);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(alignedRelease.AddDays(2)));
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithRelease(youTubeInput.Release));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, a Spotify catalogue episode " +
        "does not match when its release falls outside the catalogue tolerance window.")]
    public void spotify_catalogue_outside_tolerance_does_not_match_youtube_release_authority_stored_episode()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            release: youTubeRelease);
        var farOffSpotifyRelease = youTubeRelease.AddDays(-5);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(farOffSpotifyRelease));
        var context = new ReleaseMatchContext(podcast, stored, incoming);
        var strategy = new SpotifyCatalogueReleaseMatchStrategy();

        // Act
        var result = strategy.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, " +
        "a stored YouTube episode matches an incoming Spotify catalogue item when release aligns after delay adjustment.")]
    public void negative_delay_youtube_stored_spotify_incoming_aligned()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, incoming, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var context = new ReleaseMatchContext(podcast, stored, incoming);
        var strategy = new SpotifyCatalogueReleaseMatchStrategy();

        // Act
        var result = strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the podcast has no YouTube publishing delay, YouTube publish-delay strategy " +
        "defers to other strategies by returning null.")]
    public void zero_delay_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateSpotifyCatalogueEpisode();
        var incoming = _fixture.CreateYouTubeCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored and incoming episodes both carry YouTube identity, YouTube publish-delay strategy " +
        "defers because delay alignment applies only across platform types.")]
    public void both_youtube_identity_defers()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            TimeSpan.FromDays(1).Ticks);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateYouTubeCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the stored episode has YouTube identity but the incoming episode does not, " +
        "YouTube publish-delay strategy defers because only audio-to-YouTube alignment is in scope.")]
    public void stored_youtube_incoming_non_youtube_defers()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            TimeSpan.FromDays(1).Ticks);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode();
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, " +
        "a stored Spotify episode matches an incoming YouTube episode when publish aligns after delay adjustment.")]
    public void positive_delay_spotify_stored_youtube_incoming_aligned()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var sharedLength = _fixture.CreateDuration();
        var spotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateShortTitle();
            e.Length = sharedLength;
            e.Release = audioRelease;
            e.SpotifyId = spotifyId;
            e.Urls = new ServiceUrls { Spotify = _fixture.DefaultSpotifyUrl(spotifyId) };
        });
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(sharedLength));
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithRelease(youTubeInput.Release)
            .WithDuration(sharedLength));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }
}
