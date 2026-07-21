using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Direct characterization of <see cref="ExactReleaseMatchStrategy.Evaluate"/>.
/// </summary>
public class ExactReleaseMatchStrategyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly ExactReleaseMatchStrategy _strategy = new();

    [Fact(DisplayName =
        "When the podcast has zero YouTube publishing delay and releases are identical, " +
        "exact release strategy returns true within the fourteen-day consideration threshold.")]
    public void zero_delay_identical_releases_returns_true_within_fourteen_day_threshold()
    {
        // Arrange
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(5);
        var sharedLength = _fixture.CreateDuration();
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = sharedRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = sharedRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When raw release delta exceeds the fourteen-day consideration window but delay-adjusted YouTube publish " +
        "aligns within the offset confidence threshold, exact release strategy returns true.")]
    public void cross_platform_delay_aligned_release_returns_true_beyond_raw_consideration_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(19);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var sharedLength = _fixture.CreateDuration();
        var audioRelease = DomainTestFixture.UtcDateDaysAgo(30);
        var youTubeRelease = audioRelease.Add(publishingDelay);
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = audioRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithRelease(youTubeRelease)
            .WithDuration(sharedLength));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When audio-stored and YouTube-incoming releases share a calendar day but miss delay-aligned " +
        "expectation, exact release strategy still returns true — same-day is a moderate-confidence signal.")]
    public void cross_platform_same_calendar_day_returns_true_when_offset_misaligned()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromHours(2);
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var sharedLength = _fixture.CreateDuration();
        var audioRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(14));
        var youTubeRelease = audioRelease.AddHours(1);
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = audioRelease;
            e.Length = sharedLength;
            e.AppleId = _fixture.CreateAppleId();
        });
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithRelease(youTubeRelease)
            .WithDuration(sharedLength));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
        EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease)
            .Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the podcast has zero YouTube publishing delay and releases differ beyond tolerance, " +
        "exact release strategy returns false.")]
    public void zero_delay_releases_outside_tolerance_returns_false()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDateDaysAgo(60);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDateDaysAgo(2);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the podcast has positive YouTube publishing delay and releases align within tolerance, " +
        "exact release strategy returns true.")]
    public void positive_delay_aligned_releases_within_tolerance_returns_true()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var sharedLength = _fixture.CreateDuration();
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = audioRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = audioRelease.AddHours(1);
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the podcast has positive delay but releases differ beyond tolerance, " +
        "exact release strategy defers by returning null.")]
    public void positive_delay_releases_outside_tolerance_returns_null()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var sharedLength = _fixture.CreateDuration();
        var storedRelease = DomainTestFixture.UtcDateDaysAgo(60);
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = storedRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDateDaysAgo(2);
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the podcast has negative YouTube publishing delay, exact release strategy defers " +
        "to cross-platform strategies by returning null.")]
    public void negative_delay_returns_null()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(5);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: sharedRelease);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithRelease(sharedRelease));
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Exact release strategy uses the stored episode length as reference when it is greater than zero.")]
    public void uses_stored_length_for_tolerance_when_present()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var storedLength = TimeSpan.FromHours(2);
        var incomingLength = TimeSpan.FromMinutes(30);
        var sharedRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = sharedRelease;
            e.Length = storedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = sharedRelease.AddMinutes(30);
            e.Length = incomingLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    public static TheoryData<string> DelayAndDeltaScenarios =>
        new()
        {
            "zero_delay_within",
            "zero_delay_outside",
            "positive_delay_within",
            "positive_delay_outside_defers",
            "negative_delay_defers"
        };

    [Theory(DisplayName =
        "Exact release strategy returns true, false, or null according to delay sign " +
        "and whether the release delta falls within tolerance.")]
    [MemberData(nameof(DelayAndDeltaScenarios))]
    public void delay_sign_and_release_delta_matrix(string scenario)
    {
        // Arrange
        var sharedLength = _fixture.CreateDuration();
        Podcast podcast;
        DateTime storedRelease;
        DateTime incomingRelease;
        bool? expected;

        switch (scenario)
        {
            case "zero_delay_within":
                podcast = _fixture.CreatePodcast();
                storedRelease = DomainTestFixture.UtcDateDaysAgo(5);
                incomingRelease = storedRelease;
                expected = true;
                break;
            case "zero_delay_outside":
                podcast = _fixture.CreatePodcast();
                storedRelease = DomainTestFixture.UtcDateDaysAgo(60);
                incomingRelease = DomainTestFixture.UtcDateDaysAgo(2);
                expected = false;
                break;
            case "positive_delay_within":
                podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
                    _fixture.CreateYouTubeChannelId(),
                    TimeSpan.FromDays(1).Ticks);
                storedRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
                incomingRelease = storedRelease.AddHours(1);
                expected = true;
                break;
            case "positive_delay_outside_defers":
                podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
                    _fixture.CreateYouTubeChannelId(),
                    TimeSpan.FromDays(1).Ticks);
                storedRelease = DomainTestFixture.UtcDateDaysAgo(60);
                incomingRelease = DomainTestFixture.UtcDateDaysAgo(2);
                expected = null;
                break;
            case "negative_delay_defers":
                podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
                storedRelease = DomainTestFixture.UtcDateDaysAgo(5);
                incomingRelease = storedRelease;
                expected = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var stored = _fixture.CreateEpisode(e =>
        {
            e.Release = storedRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Release = incomingRelease;
            e.Length = sharedLength;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var context = new ReleaseMatchContext(podcast, stored, incoming);

        // Act
        var result = _strategy.Evaluate(context);

        // Assert
        result.Should().Be(expected);
    }
}
