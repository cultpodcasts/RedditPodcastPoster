using FluentAssertions;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Characterizes <see cref="EpisodeReleaseTolerance"/> release lookup and catalogue tolerance behavior.
/// </summary>
public class EpisodeReleaseToleranceRules
{
    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<string> ToleranceScenarioNames =>
        new()
        {
            "zero_delay",
            "negative_delay",
            "positive_delay_youtube_authority",
            "positive_delay_spotify_authority"
        };

    [Theory(DisplayName =
        "GetToleranceTicks returns expected thresholds for delay and release-authority scenarios.")]
    [MemberData(nameof(ToleranceScenarioNames))]
    public void get_tolerance_ticks_for_delay_and_authority_scenarios(string scenario)
    {
        // Arrange
        var episodeLength = _fixture.CreateDuration();
        var podcast = scenario switch
        {
            "zero_delay" => _fixture.CreatePodcast(),
            "negative_delay" => _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay(),
            "positive_delay_youtube_authority" => _fixture.CreateYouTubeReleaseAuthorityPodcast(
                _fixture.CreateYouTubeChannelId(),
                TimeSpan.FromDays(1).Ticks),
            "positive_delay_spotify_authority" => CreateSpotifyPrimaryWithPositiveDelay(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        // Act
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, episodeLength);
        var tolerance = TimeSpan.FromTicks(toleranceTicks);

        // Assert
        switch (scenario)
        {
            case "zero_delay":
                tolerance.Should().Be(EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold);
                break;
            case "negative_delay":
                tolerance.Should().BeGreaterThan(EpisodeReleaseTolerance.YouTubePublishDelayMatchThreshold);
                break;
            case "positive_delay_youtube_authority":
                tolerance.Should().BeLessThan(EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold);
                tolerance.Should().BeGreaterThan(TimeSpan.FromDays(1));
                break;
            case "positive_delay_spotify_authority":
                tolerance.Should().Be(EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold);
                break;
        }
    }

    [Fact(DisplayName =
        "When YouTube is release authority with negative delay, Spotify catalogue day tolerance " +
        "is five days and SpotifyCatalogueReleaseMatches accepts releases within that window.")]
    public void spotify_catalogue_day_tolerance_for_negative_delay_youtube_authority()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        var withinFiveDays = expectedRelease.AddDays(-4);
        var beyondFiveDays = expectedRelease.AddDays(-6);

        // Act
        var dayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var withinMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            withinFiveDays, expectedRelease, toleranceTicks, podcast);
        var beyondMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            beyondFiveDays, expectedRelease, toleranceTicks, podcast);

        // Assert
        dayTolerance.Should().Be(5);
        withinMatches.Should().BeTrue();
        beyondMatches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the podcast is not YouTube release authority, Spotify catalogue day tolerance " +
        "is one day and SpotifyCatalogueReleaseMatches uses that window.")]
    public void spotify_catalogue_day_tolerance_for_default_podcast()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var expectedRelease = DomainTestFixture.UtcDateDaysAgo(10);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        var withinOneDay = expectedRelease.AddDays(1);
        var beyondOneDay = expectedRelease.AddDays(15);

        // Act
        var dayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var withinMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            withinOneDay, expectedRelease, toleranceTicks, podcast);
        var beyondMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            beyondOneDay, expectedRelease, toleranceTicks, podcast);

        // Assert
        dayTolerance.Should().Be(1);
        withinMatches.Should().BeTrue();
        beyondMatches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "SpotifyCatalogueReleaseMatches accepts aligned Spotify catalogue dates for " +
        "YouTube release authority negative-delay podcasts.")]
    public void spotify_catalogue_release_matches_negative_delay_alignment()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(
            -youTubeReleaseDaysAgo,
            _fixture.CreateNonMidnightTimeOfDay());
        var expectedAudioRelease = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(
            podcast,
            youTubeRelease,
            episodeHasYouTubeIdentity: true);
        var spotifyCatalogueRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            spotifyDaysAfterYouTube);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            spotifyCatalogueRelease,
            expectedAudioRelease,
            toleranceTicks,
            podcast);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "SpotifyCatalogueReleaseMatches rejects far-off Spotify catalogue dates.")]
    public void spotify_catalogue_release_matches_outside_tolerance()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var farOffSpotifyRelease = expectedRelease.AddDays(-30);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            farOffSpotifyRelease,
            expectedRelease,
            toleranceTicks,
            podcast);

        // Assert
        matches.Should().BeFalse();
    }

    private Podcast CreateSpotifyPrimaryWithPositiveDelay()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks;
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        return podcast;
    }
}
