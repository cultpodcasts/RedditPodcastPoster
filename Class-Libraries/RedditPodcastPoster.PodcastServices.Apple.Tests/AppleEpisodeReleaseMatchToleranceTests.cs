using FluentAssertions;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class EpisodeReleaseToleranceTests
{
    private readonly DomainTestFixture _fixture = new();
    [Fact]
    public void GetToleranceTicks_WhenYouTubeIsReleaseAuthorityWithPositiveDelay_UsesTwiceDelayNotFourteenDays()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.FromHours(1)));

        tolerance.Should().BeLessThan(TimeSpan.FromDays(14));
        tolerance.Should().BeGreaterThan(TimeSpan.FromDays(1));
    }

    [Fact]
    public void SpotifyCatalogueReleaseMatches_WhenSpotifyDateIsMidnightAndExpectedHasTimeWithinOneDay_ReturnsTrue()
    {
        var expected = new DateTime(2026, 7, 2, 9, 15, 27, DateTimeKind.Utc);
        var spotifyCatalogue = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(spotifyCatalogue, expected).Should().BeTrue();
    }

    [Fact]
    public void GetToleranceTicks_WhenNoYouTubeDelay_UsesFourteenDayThreshold()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.Zero));

        tolerance.Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public void EpisodesReleaseMatch_WhenStoredAudioAndYouTubeIncomingDifferByPublishingDelay_ReturnsTrue()
    {
        var podcast = new Podcast { YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks };
        var audioRelease = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = new Episode
        {
            Release = audioRelease,
            Length = TimeSpan.FromHours(1),
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/test") }
        };
        var incoming = new Episode
        {
            Release = audioRelease.AddDays(1),
            Length = TimeSpan.FromHours(1),
            YouTubeId = "test-video",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=test-video") }
        };

        EpisodeDomainTestServices.CreateMatcher()
            .IsMatch(existing, incoming, episodeMatchRegex: null, podcast)
            .Should().BeTrue();
    }

    [Fact]
    public void SpotifyCatalogueReleaseMatches_WhenYouTubeReleaseAuthorityWithNegativeDelayAndSpotifyLandsEarlyWithinFiveDays_ReturnsTrue()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12)).Ticks
        };
        var expected = new DateTime(2026, 7, 6, 1, 8, 6, DateTimeKind.Utc);
        var spotifyCatalogue = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var tolerance = EpisodeReleaseTolerance.GetToleranceTicks(podcast, TimeSpan.FromMinutes(88));

        EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
                spotifyCatalogue,
                expected,
                tolerance,
                podcast)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldEnrichDespiteReleaseWindow_WhenYouTubeOnlyEpisodeNearExpectedAudioRelease_ReturnsTrue()
    {
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

        EpisodeReleaseTolerance.ShouldEnrichDespiteReleaseWindow(episode, podcast).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenYouTubeAuthorityStoredYouTubeAndSpotifyIncomingAlignAfterDelayAdjustment_ReturnsTrue()
    {
        var delay = TimeSpan.FromDays(1);
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = delay.Ticks
        };
        var audioRelease = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = new Episode
        {
            Release = audioRelease.Add(delay),
            Length = TimeSpan.FromHours(1),
            YouTubeId = "test-video",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=test-video") }
        };
        var incoming = new Episode
        {
            Release = audioRelease,
            Length = TimeSpan.FromHours(1),
            SpotifyId = "spotify-id",
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/test") }
        };

        EpisodeDomainTestServices.CreateMatcher()
            .IsMatch(existing, incoming, episodeMatchRegex: null, podcast)
            .Should().BeTrue();
    }

    [Fact]
    public void GetAudioReleaseForPlatformLookup_WhenEpisodeDiscoveredViaYouTubeOnSpotifyPrimaryPodcast_SubtractsPublishingDelay()
    {
        var delay = TimeSpan.FromHours(9);
        var podcast = new Podcast
        {
            SpotifyId = "spotify-show",
            YouTubePublicationOffset = delay.Ticks
        };
        var youTubePublish = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc);
        var episode = new Episode
        {
            Release = youTubePublish,
            YouTubeId = "video-id",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=video-id") }
        };

        EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode)
            .Should().Be(youTubePublish - delay);
    }

    [Fact]
    public void GetAudioReleaseForPlatformLookup_WhenYouTubeIsReleaseAuthority_SubtractsPublishingDelay()
    {
        var delay = TimeSpan.FromDays(1);
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = delay.Ticks
        };
        var release = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc);

        EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, release, episodeHasYouTubeIdentity: false)
            .Should().Be(release - delay);
    }

    [Fact]
    public void GetAudioReleaseForPlatformLookup_WhenYouTubeAuthorityEpisodeMergedWithSpotify_SubtractsPublishingDelay()
    {
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

        EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode)
            .Should().Be(youTubeRelease - TimeSpan.FromTicks(c2cDelayTicks));
    }

    [Fact]
    public void ShouldPreserveYouTubeAuthoritativeRelease_WhenYouTubeReleaseAuthorityEpisodeHasYouTubeIdentity_ReturnsTrue()
    {
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

        EpisodeReleaseTolerance.ShouldPreserveYouTubeAuthoritativeRelease(podcast, episode)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPreserveYouTubeAuthoritativeRelease_WhenSpotifyOnlyEpisode_ReturnsFalse()
    {
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

        EpisodeReleaseTolerance.ShouldPreserveYouTubeAuthoritativeRelease(podcast, episode)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void SpotifyCatalogueReleaseMatches_WhenYouTubeReleaseAuthorityEpisodeAlignsWithinTolerance_ReturnsTrue(
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
        var tolerance = EpisodeReleaseTolerance.GetToleranceTicks(
            podcast,
            _fixture.CreateDuration());

        // Act & Assert
        EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
                appleCatalogueRelease,
                expectedAudioRelease,
                tolerance,
                podcast)
            .Should().BeTrue();
    }

    [Fact]
    public void SameUtcCalendarDate_WhenDatesMatch_ReturnsTrue()
    {
        var midnight = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var withTime = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);

        DateOnly.FromDateTime(midnight).Should().Be(DateOnly.FromDateTime(withTime));
    }

    [Fact]
    public void SameUtcCalendarDate_WhenDatesDiffer_ReturnsFalse()
    {
        var dayOne = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayTwo = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);

        DateOnly.FromDateTime(dayOne).Should().NotBe(DateOnly.FromDateTime(dayTwo));
    }
}
