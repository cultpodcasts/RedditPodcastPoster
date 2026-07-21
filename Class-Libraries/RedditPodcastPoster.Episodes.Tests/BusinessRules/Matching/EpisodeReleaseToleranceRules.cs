using FluentAssertions;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

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
        "is five days around expected audio, and audio earlier than that but still after the inferred " +
        "YouTube day is accepted (early-within-configured-delay). Audio before YouTube is rejected.")]
    public void spotify_catalogue_day_tolerance_for_negative_delay_youtube_authority()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = expectedRelease.Add(delay);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        var withinFiveDays = expectedRelease.AddDays(-4);
        var earlyWithinDelayWindow = youTubeRelease.AddDays(5);
        var beforeYouTube = youTubeRelease.AddDays(-2);

        // Act
        var dayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var withinMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            withinFiveDays, expectedRelease, toleranceTicks, podcast);
        var earlyMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            earlyWithinDelayWindow, expectedRelease, toleranceTicks, podcast);
        var beforeYouTubeMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            beforeYouTube, expectedRelease, toleranceTicks, podcast);

        // Assert
        dayTolerance.Should().Be(5);
        withinMatches.Should().BeTrue();
        earlyMatches.Should().BeTrue();
        beforeYouTubeMatches.Should().BeFalse();
        EpisodeReleaseTolerance.IsAudioWithinNegativeDelayYouTubeToExpectedWindow(
                earlyWithinDelayWindow, expectedRelease, podcast)
            .Should().BeTrue();
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
        "IsYouTubePublishDelayAligned returns true when YouTube publish is within one day of audio release plus offset " +
        "and false when the delta exceeds that confidence window.")]
    public void is_youtube_publish_delay_aligned_uses_one_day_confidence_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromHours(2);
        var audioRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(14));
        var alignedYouTubeRelease = audioRelease.Add(publishingDelay);
        var misalignedYouTubeRelease = alignedYouTubeRelease.AddDays(2);

        // Act
        var aligned = EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
            audioRelease, alignedYouTubeRelease, publishingDelay);
        var misaligned = EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
            audioRelease, misalignedYouTubeRelease, publishingDelay);

        // Assert
        aligned.Should().BeTrue();
        misaligned.Should().BeFalse();
    }

    [Fact(DisplayName =
        "AreCrossPlatformReleasesOnSameCalendarDay returns true when audio and YouTube share a calendar day " +
        "regardless of whether the configured offset would predict a later YouTube publish.")]
    public void are_cross_platform_releases_on_same_calendar_day_ignores_offset_expectation()
    {
        // Arrange
        var audioRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(14));
        var youTubeReleaseSameDay = audioRelease.AddHours(1);

        // Act
        var sameDay = EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(
            audioRelease, youTubeReleaseSameDay);
        var differentDay = EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(
            audioRelease, audioRelease.AddDays(1));

        // Assert
        sameDay.Should().BeTrue();
        differentDay.Should().BeFalse();
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
        "SpotifyCatalogueReleaseMatches rejects Spotify catalogue dates before the inferred YouTube " +
        "publish day for negative-delay YouTube-authority podcasts.")]
    public void spotify_catalogue_release_matches_outside_tolerance()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var expectedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var youTubeRelease = expectedRelease.Add(podcast.YouTubePublishingDelay());
        var farOffSpotifyRelease = youTubeRelease.AddDays(-5);
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

    [Fact(DisplayName =
        "For a YouTube-authority podcast with a large negative publication offset (−31.5d) when audio " +
        "arrives ~13d after YouTube, SpotifyCatalogueReleaseMatches accepts that early date against " +
        "expected audio = YouTube − delay.")]
    public void spotify_catalogue_accepts_early_audio_within_configured_negative_delay()
    {
        // Arrange — large negative offset; actual audio early within the configured delay window
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12)).Ticks;
        var youTubeRelease = new DateTime(2026, 7, 1, 15, 21, 27, DateTimeKind.Utc);
        var audioRelease = new DateTime(2026, 7, 14, 13, 0, 0, DateTimeKind.Utc);
        var expectedAudioRelease = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(
            podcast,
            youTubeRelease,
            episodeHasYouTubeIdentity: true);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.FromMinutes(80));

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            audioRelease,
            expectedAudioRelease,
            toleranceTicks,
            podcast);

        // Assert
        expectedAudioRelease.Date.Should().Be(new DateTime(2026, 8, 2));
        Math.Abs((audioRelease - youTubeRelease).TotalDays).Should().BeApproximately(12.9, 0.1);
        matches.Should().BeTrue();
    }

    private Podcast CreateSpotifyPrimaryWithPositiveDelay()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks;
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        return podcast;
    }

    public static TheoryData<string> NullPodcastToleranceScenarioNames =>
        new()
        {
            "zero_delay",
            "negative_delay",
            "positive_delay_youtube_authority",
            "positive_delay_spotify_authority"
        };

    [Theory(DisplayName =
        "GetToleranceTicks without a podcast uses delay and release-authority parameters " +
        "with the same thresholds as the podcast overload.")]
    [MemberData(nameof(NullPodcastToleranceScenarioNames))]
    public void get_tolerance_ticks_without_podcast_mirrors_podcast_overload(string scenario)
    {
        // Arrange
        var episodeLength = _fixture.CreateDuration();
        var (podcast, delay, authority) = scenario switch
        {
            "zero_delay" => (_fixture.CreatePodcast(), (TimeSpan?)null, (Service?)null),
            "negative_delay" => (
                _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay(),
                _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay().YouTubePublishingDelay(),
                Service.YouTube),
            "positive_delay_youtube_authority" => (
                _fixture.CreateYouTubeReleaseAuthorityPodcast(
                    _fixture.CreateYouTubeChannelId(),
                    TimeSpan.FromDays(1).Ticks),
                TimeSpan.FromDays(1),
                Service.YouTube),
            "positive_delay_spotify_authority" => (
                CreateSpotifyPrimaryWithPositiveDelay(),
                TimeSpan.FromDays(1),
                Service.Spotify),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        // Act
        var fromPodcast = EpisodeReleaseTolerance.GetToleranceTicks(podcast, episodeLength);
        var withoutPodcast = EpisodeReleaseTolerance.GetToleranceTicks(
            null,
            episodeLength,
            delay,
            authority);

        // Assert
        withoutPodcast.Should().Be(fromPodcast);
    }

    [Fact(DisplayName =
        "SpotifyCatalogueReleaseMatches accepts a midnight Spotify catalogue date when the expected " +
        "release is the same calendar day with a non-midnight time.")]
    public void spotify_catalogue_release_matches_midnight_spotify_date_same_calendar_day()
    {
        // Arrange
        var expected = new DateTime(2026, 7, 2, 9, 15, 27, DateTimeKind.Utc);
        var spotifyCatalogue = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(spotifyCatalogue, expected);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When YouTube is release authority with negative delay, Spotify catalogue release may land " +
        "several days early and still match within the five-day day tolerance.")]
    public void spotify_catalogue_release_matches_negative_delay_early_spotify_within_five_days()
    {
        // Arrange
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12)).Ticks
        };
        var expected = new DateTime(2026, 7, 6, 1, 8, 6, DateTimeKind.Utc);
        var spotifyCatalogue = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var tolerance = EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.FromMinutes(88));

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            spotifyCatalogue,
            expected,
            tolerance,
            podcast);

        // Assert
        matches.Should().BeTrue();
    }

    public static TheoryData<string> AudioReleaseLookupScenarioNames =>
        new()
        {
            "zero_delay",
            "youtube_authority",
            "youtube_discovered_on_spotify_primary",
            "youtube_authority_without_episode_identity",
            "youtube_identity_without_audio_platform"
        };

    [Theory(DisplayName =
        "GetAudioReleaseForPlatformLookup adjusts release by publishing delay only when authority " +
        "or episode/platform configuration requires it.")]
    [MemberData(nameof(AudioReleaseLookupScenarioNames))]
    public void get_audio_release_for_platform_lookup_scenarios(string scenario)
    {
        // Arrange
        var delay = TimeSpan.FromDays(1);
        var release = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc);
        var expected = release;

        Podcast podcast;
        Episode? episode = null;
        switch (scenario)
        {
            case "zero_delay":
                podcast = _fixture.CreatePodcast();
                break;
            case "youtube_authority":
                podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
                    _fixture.CreateYouTubeChannelId(),
                    delay.Ticks);
                expected = release - delay;
                break;
            case "youtube_discovered_on_spotify_primary":
                podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
                podcast.YouTubePublicationOffset = delay.Ticks;
                episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
                    podcast,
                    release: release);
                expected = release - delay;
                break;
            case "youtube_authority_without_episode_identity":
                podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
                    _fixture.CreateYouTubeChannelId(),
                    delay.Ticks);
                expected = release - delay;
                break;
            case "youtube_identity_without_audio_platform":
                podcast = _fixture.CreatePodcast();
                podcast.YouTubePublicationOffset = delay.Ticks;
                episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
                    podcast,
                    release: release);
                expected = release;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        // Act
        var lookup = episode == null
            ? EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(
                podcast,
                release,
                episodeHasYouTubeIdentity: false)
            : EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

        // Assert
        lookup.Should().Be(expected);
    }

    [Fact(DisplayName =
        "GetAudioReleaseForPlatformLookup subtracts publishing delay when a merged episode has both " +
        "YouTube and Spotify identities on a YouTube release authority podcast.")]
    public void get_audio_release_for_platform_lookup_merged_youtube_and_spotify_episode()
    {
        // Arrange
        const long c2cDelayTicks = -27216000000000;
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = c2cDelayTicks,
            SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH"
        };
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var episode = new Episode
        {
            Release = youTubeRelease,
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4",
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri("https://open.spotify.com/episode/6O1Z1s7ca0PI8Gq1rdt3j4")
            }
        };

        // Act
        var lookup = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

        // Assert
        lookup.Should().Be(youTubeRelease - TimeSpan.FromTicks(c2cDelayTicks));
    }

    [Fact(DisplayName =
        "ShouldPreserveYouTubeAuthoritativeRelease returns true when YouTube is release authority " +
        "and the episode has YouTube identity.")]
    public void should_preserve_youtube_authoritative_release_when_youtube_identity_present()
    {
        // Arrange
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = -27216000000000
        };
        var episode = new Episode
        {
            Release = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4"
        };

        // Act
        var preserve = EpisodeReleaseTolerance.ShouldPreserveYouTubeAuthoritativeRelease(podcast, episode);

        // Assert
        preserve.Should().BeTrue();
    }

    [Fact(DisplayName =
        "ShouldPreserveYouTubeAuthoritativeRelease returns false when the episode has no YouTube identity.")]
    public void should_preserve_youtube_authoritative_release_false_for_spotify_only_episode()
    {
        // Arrange
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = -27216000000000
        };
        var episode = new Episode
        {
            Release = new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4"
        };

        // Act
        var preserve = EpisodeReleaseTolerance.ShouldPreserveYouTubeAuthoritativeRelease(podcast, episode);

        // Assert
        preserve.Should().BeFalse();
    }

    [Fact(DisplayName =
        "ShouldEnrichDespiteReleaseWindow returns true for a YouTube-only episode near expected audio release " +
        "when Spotify or Apple catalogue IDs are still missing.")]
    public void should_enrich_despite_release_window_when_youtube_only_near_expected_audio_release()
    {
        // Arrange
        var delay = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12));
        var expectedAudioRelease = DateTime.UtcNow.AddDays(4);
        var youTubeRelease = expectedAudioRelease.Add(delay);
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = delay.Ticks,
            SpotifyId = "show-id"
        };
        var episode = new Episode
        {
            Release = youTubeRelease,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };

        // Act
        var shouldEnrich = EpisodeReleaseTolerance.ShouldEnrichDespiteReleaseWindow(episode, podcast);

        // Assert
        shouldEnrich.Should().BeTrue();
    }

    public static TheoryData<string> ShouldNotEnrichDespiteReleaseWindowScenarioNames =>
        new()
        {
            "positive_delay",
            "spotify_authority",
            "episode_already_has_spotify_id",
            "outside_release_window"
        };

    [Theory(DisplayName =
        "ShouldEnrichDespiteReleaseWindow returns false when delay, authority, platform IDs, " +
        "or the current time falls outside the enrichment window.")]
    [MemberData(nameof(ShouldNotEnrichDespiteReleaseWindowScenarioNames))]
    public void should_enrich_despite_release_window_returns_false(string scenario)
    {
        // Arrange
        var delay = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12));
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = delay.Ticks,
            SpotifyId = "show-id"
        };
        var episode = new Episode
        {
            Release = DateTime.UtcNow.AddDays(4).Add(delay),
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };

        switch (scenario)
        {
            case "positive_delay":
                podcast.YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks;
                break;
            case "spotify_authority":
                podcast.ReleaseAuthority = Service.Spotify;
                break;
            case "episode_already_has_spotify_id":
                episode.SpotifyId = "existing-spotify-id";
                break;
            case "outside_release_window":
                episode.Release = DateTime.UtcNow.AddDays(-60).Add(delay);
                break;
        }

        // Act
        var shouldEnrich = EpisodeReleaseTolerance.ShouldEnrichDespiteReleaseWindow(episode, podcast);

        // Assert
        shouldEnrich.Should().BeFalse();
    }

    [Theory(DisplayName =
        "SpotifyCatalogueReleaseMatches accepts Apple catalogue releases within tolerance " +
        "for YouTube release authority negative-delay podcasts.")]
    [InlineData(0)]
    [InlineData(4)]
    public void spotify_catalogue_release_matches_youtube_authority_apple_within_tolerance(
        int appleCatalogueDaysAfterSpotifyDate)
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
        var spotifyCatalogueDate = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            spotifyDaysAfterYouTube);
        var appleCatalogueRelease = spotifyCatalogueDate.AddDays(appleCatalogueDaysAfterSpotifyDate)
            .AddHours(8);
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(
            podcast,
            _fixture.CreateDuration());

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            appleCatalogueRelease,
            expectedAudioRelease,
            toleranceTicks,
            podcast);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "GetToleranceTicks for positive-delay YouTube authority omits episode length from tolerance " +
        "when the stored episode length is zero.")]
    public void get_tolerance_ticks_positive_delay_youtube_authority_ignores_zero_episode_length()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            TimeSpan.FromDays(1).Ticks);
        var withLength = EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.FromHours(1));
        var withoutLength = EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.Zero);

        // Act & assert
        withoutLength.Should().BeLessThan(withLength);
        withoutLength.Should().BeGreaterThan(TimeSpan.FromDays(1).Ticks);
    }

    [Fact(DisplayName =
        "ShouldEnrichDespiteReleaseWindow returns true when only Apple catalogue identity is still missing " +
        "for a YouTube-only episode inside the enrichment window.")]
    public void should_enrich_despite_release_window_when_only_apple_id_missing()
    {
        // Arrange
        var delay = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12));
        var expectedAudioRelease = DateTime.UtcNow.AddDays(4);
        var youTubeRelease = expectedAudioRelease.Add(delay);
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = delay.Ticks,
            AppleId = _fixture.CreateAppleId()
        };
        var episode = new Episode
        {
            Release = youTubeRelease,
            YouTubeId = _fixture.CreateYouTubeId(),
            Urls = new ServiceUrls
            {
                YouTube = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}")
            }
        };

        // Act
        var shouldEnrich = EpisodeReleaseTolerance.ShouldEnrichDespiteReleaseWindow(episode, podcast);

        // Assert
        shouldEnrich.Should().BeTrue();
    }
}
