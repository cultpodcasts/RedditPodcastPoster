using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class EpisodeReleaseMatchToleranceTests
{
    [Fact]
    public void GetToleranceTicks_WhenYouTubeIsReleaseAuthorityWithPositiveDelay_UsesTwiceDelayNotFourteenDays()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, TimeSpan.FromHours(1)));

        tolerance.Should().BeLessThan(TimeSpan.FromDays(14));
        tolerance.Should().BeGreaterThan(TimeSpan.FromDays(1));
    }

    [Fact]
    public void GetToleranceTicks_WhenNoYouTubeDelay_UsesFourteenDayThreshold()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, TimeSpan.Zero));

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

        EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existing, incoming).Should().BeTrue();
    }

    [Fact]
    public void EpisodesReleaseMatch_WhenYouTubeAuthorityStoredYouTubeAndSpotifyIncomingAlignAfterDelayAdjustment_ReturnsTrue()
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

        EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existing, incoming).Should().BeTrue();
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

        EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, episode)
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

        EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, release, episodeHasYouTubeIdentity: false)
            .Should().Be(release - delay);
    }
}
