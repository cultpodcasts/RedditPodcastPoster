using FluentAssertions;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Domain <see cref="EpisodeReleaseTolerance"/> parity with legacy
/// <see cref="EpisodeReleaseMatchTolerance"/> before Abstractions type removal.
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
        "Domain GetToleranceTicks matches legacy EpisodeReleaseMatchTolerance for delay and authority scenarios.")]
    [MemberData(nameof(ToleranceScenarioNames))]
    public void get_tolerance_ticks_matches_legacy_implementation(string scenario)
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
        var domainTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, episodeLength);
        var legacyTicks = EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, episodeLength);

        // Assert
        domainTicks.Should().Be(legacyTicks);
    }

    [Fact(DisplayName =
        "When YouTube is release authority with negative delay, Spotify catalogue day tolerance " +
        "is five days in domain and legacy SpotifyCatalogueReleaseMatches behavior.")]
    public void spotify_catalogue_day_tolerance_matches_legacy_for_negative_delay_youtube_authority()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        var withinFiveDays = expectedRelease.AddDays(-4);
        var beyondFiveDays = expectedRelease.AddDays(-6);

        // Act
        var domainDayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var domainWithin = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            withinFiveDays, expectedRelease, toleranceTicks, podcast);
        var domainBeyond = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            beyondFiveDays, expectedRelease, toleranceTicks, podcast);
        var legacyWithin = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            withinFiveDays, expectedRelease, toleranceTicks, podcast);
        var legacyBeyond = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            beyondFiveDays, expectedRelease, toleranceTicks, podcast);

        // Assert
        domainDayTolerance.Should().Be(5);
        domainWithin.Should().BeTrue().And.Be(legacyWithin);
        domainBeyond.Should().BeFalse().And.Be(legacyBeyond);
    }

    [Fact(DisplayName =
        "When the podcast is not YouTube release authority, Spotify catalogue day tolerance " +
        "is one day in domain and legacy SpotifyCatalogueReleaseMatches behavior.")]
    public void spotify_catalogue_day_tolerance_matches_legacy_for_default_podcast()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var expectedRelease = DomainTestFixture.UtcDateDaysAgo(10);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        var withinOneDay = expectedRelease.AddDays(1);
        var beyondOneDay = expectedRelease.AddDays(15);

        // Act
        var domainDayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var domainWithin = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            withinOneDay, expectedRelease, toleranceTicks, podcast);
        var domainBeyond = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            beyondOneDay, expectedRelease, toleranceTicks, podcast);
        var legacyWithin = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            withinOneDay, expectedRelease, toleranceTicks, podcast);
        var legacyBeyond = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            beyondOneDay, expectedRelease, toleranceTicks, podcast);

        // Assert
        domainDayTolerance.Should().Be(1);
        domainWithin.Should().BeTrue().And.Be(legacyWithin);
        domainBeyond.Should().BeFalse().And.Be(legacyBeyond);
    }

    [Fact(DisplayName =
        "SpotifyCatalogueReleaseMatches day and tick tolerance outcomes match legacy " +
        "for YouTube release authority negative-delay alignment.")]
    public void spotify_catalogue_release_matches_matches_legacy_negative_delay_alignment()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(
            -youTubeReleaseDaysAgo,
            _fixture.CreateNonMidnightTimeOfDay());
        var expectedAudioRelease = EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(
            podcast,
            youTubeRelease,
            episodeHasYouTubeIdentity: true);
        var spotifyCatalogueRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            spotifyDaysAfterYouTube);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());

        // Act
        var domainMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            spotifyCatalogueRelease,
            expectedAudioRelease,
            toleranceTicks,
            podcast);
        var legacyMatches = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            spotifyCatalogueRelease,
            expectedAudioRelease,
            toleranceTicks,
            podcast);

        // Assert
        domainMatches.Should().BeTrue();
        domainMatches.Should().Be(legacyMatches);
    }

    [Fact(DisplayName =
        "SpotifyCatalogueReleaseMatches rejects far-off Spotify catalogue dates consistently " +
        "in domain and legacy implementations.")]
    public void spotify_catalogue_release_matches_matches_legacy_outside_tolerance()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var farOffSpotifyRelease = expectedRelease.AddDays(-30);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());

        // Act
        var domainMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            farOffSpotifyRelease,
            expectedRelease,
            toleranceTicks,
            podcast);
        var legacyMatches = EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
            farOffSpotifyRelease,
            expectedRelease,
            toleranceTicks,
            podcast);

        // Assert
        domainMatches.Should().BeFalse();
        domainMatches.Should().Be(legacyMatches);
    }

    private Podcast CreateSpotifyPrimaryWithPositiveDelay()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks;
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        return podcast;
    }
}
