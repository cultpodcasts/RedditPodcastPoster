using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class PodcastExtensionsTests
{
    [Fact]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenReleaseAuthorityIsYouTube_ReturnsTrue()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubeChannelId = "channel",
            SpotifyId = "spotify",
            AppleId = 123
        };

        podcast.DependsOnYouTubeForEpisodeDiscovery().Should().BeTrue();
    }

    [Fact]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenChannelOnly_ReturnsTrue()
    {
        var podcast = new Podcast
        {
            YouTubeChannelId = "channel"
        };

        podcast.DependsOnYouTubeForEpisodeDiscovery().Should().BeTrue();
    }

    [Fact]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenChannelOnlyAndYouTubeSkipped_ReturnsTrue()
    {
        var podcast = new Podcast { YouTubeChannelId = "channel" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: true);

        podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext).Should().BeTrue();
    }

    [Fact]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenYouTubeEnabled_ReturnsFalse()
    {
        var podcast = new Podcast { YouTubeChannelId = "channel" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: false);

        podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext).Should().BeFalse();
    }

    [Fact]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenSpotifyDiscoversEpisodes_ReturnsFalse()
    {
        var podcast = new Podcast { YouTubeChannelId = "channel", SpotifyId = "spotify" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: true);

        podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext).Should().BeFalse();
    }

    [Fact]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenSpotifyDiscoversEpisodes_ReturnsFalse()
    {
        var podcast = new Podcast
        {
            SpotifyId = "spotify",
            YouTubeChannelId = "channel"
        };

        podcast.DependsOnYouTubeForEpisodeDiscovery().Should().BeFalse();
    }

    [Fact]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenAppleDiscoversEpisodes_ReturnsFalse()
    {
        var podcast = new Podcast
        {
            AppleId = 123,
            YouTubeChannelId = "channel"
        };

        podcast.DependsOnYouTubeForEpisodeDiscovery().Should().BeFalse();
    }

    [Fact]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityAndAudioNotYetDue_ReturnsTrue()
    {
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };
        var episode = new Episode
        {
            Release = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromHours(1),
            Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=test") }
        };

        podcast.IsDelayedYouTubePublishing(episode).Should().BeTrue();
    }

    [Fact]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityAndAudioWindowHasPassed_ReturnsFalse()
    {
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks
        };
        var episode = new Episode
        {
            Release = DateTime.UtcNow.AddDays(-2),
            Length = TimeSpan.FromHours(1),
            Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=test") }
        };

        podcast.IsDelayedYouTubePublishing(episode).Should().BeFalse();
    }

    [Fact]
    public void IsAwaitingDelayedAudioRelease_WhenYouTubeIsReleaseAuthorityAndAudioNotYetDue_ReturnsTrue()
    {
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };

        podcast.IsAwaitingDelayedAudioRelease(
                DateTime.UtcNow.AddHours(-2),
                TimeSpan.FromHours(1))
            .Should().BeTrue();
    }
}
