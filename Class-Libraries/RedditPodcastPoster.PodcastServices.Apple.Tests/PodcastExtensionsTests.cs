using FluentAssertions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class PodcastExtensionsTests
{
    [Fact(DisplayName =
        "When YouTube is the release authority, the podcast depends on YouTube for episode discovery " +
        "even though Spotify and Apple ids are set.")]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenReleaseAuthorityIsYouTube_ReturnsTrue()
    {
        // Arrange
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubeChannelId = "channel",
            SpotifyId = "spotify",
            AppleId = 123
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a podcast only has a YouTube channel, it depends on YouTube for episode discovery.")]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenChannelOnly_ReturnsTrue()
    {
        // Arrange
        var podcast = new Podcast
        {
            YouTubeChannelId = "channel"
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a channel-only podcast is indexed with YouTube URL resolving skipped, scheduled YouTube discovery is bypassed.")]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenChannelOnlyAndYouTubeSkipped_ReturnsTrue()
    {
        // Arrange
        var podcast = new Podcast { YouTubeChannelId = "channel" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: true);

        // Act
        var result = podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When YouTube URL resolving is enabled, scheduled YouTube discovery is not bypassed.")]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenYouTubeEnabled_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast { YouTubeChannelId = "channel" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: false);

        // Act
        var result = podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Spotify discovers the podcast's episodes, skipping YouTube URL resolving does not bypass discovery.")]
    public void IsScheduledYouTubeDiscoveryBypassed_WhenSpotifyDiscoversEpisodes_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast { YouTubeChannelId = "channel", SpotifyId = "spotify" };
        var indexingContext = new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipYouTubeUrlResolving: true);

        // Act
        var result = podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Spotify discovers the podcast's episodes, the podcast does not depend on YouTube for discovery.")]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenSpotifyDiscoversEpisodes_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast
        {
            SpotifyId = "spotify",
            YouTubeChannelId = "channel"
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Apple discovers the podcast's episodes, the podcast does not depend on YouTube for discovery.")]
    public void DependsOnYouTubeForEpisodeDiscovery_WhenAppleDiscoversEpisodes_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast
        {
            AppleId = 123,
            YouTubeChannelId = "channel"
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast has a positive publishing delay and its YouTube episode is inside that window, " +
        "it is not delayed, because a positive delay means the audio went out before YouTube (#1375).")]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityWithPositiveDelayInsideWindow_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromHours(1),
            Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=test") }
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast's episode was found from audio and still has no YouTube URL inside the positive delay, " +
        "it is delayed, because its YouTube upload is still due.")]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityAndAudioEpisodeAwaitingYouTubeUpload_ReturnsTrue()
    {
        // Arrange
        var podcast = new Podcast
        {
            SpotifyId = "spotify",
            YouTubeChannelId = "channel",
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromHours(6).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromHours(1),
            Urls = new ServiceUrls()
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast has a negative publishing delay, its YouTube episode is not delayed.")]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityWithNegativeDelay_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromHours(-6).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromHours(1),
            Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=test") }
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast's delay window has passed, its episode is not delayed.")]
    public void IsDelayedYouTubePublishing_WhenYouTubeIsReleaseAuthorityAndAudioWindowHasPassed_ReturnsFalse()
    {
        // Arrange
        var podcast = new Podcast
        {
            AppleId = 123,
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddDays(-2),
            Length = TimeSpan.FromHours(1),
            Urls = { YouTube = new Uri("https://www.youtube.com/watch?v=test") }
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an audio-first podcast's YouTube delay has passed, its episode is not delayed, however long the episode is.")]
    public void IsDelayedYouTubePublishing_WhenAudioPodcastAwaitingYouTubeAndDelayHasPassed_IgnoresEpisodeLength()
    {
        // Arrange
        var podcast = new Podcast
        {
            SpotifyId = "spotify",
            AppleId = 123,
            YouTubeChannelId = "channel",
            YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(51),
            Urls = new ServiceUrls()
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an audio-first podcast's episode is still inside its YouTube delay with no YouTube URL, it is delayed.")]
    public void IsDelayedYouTubePublishing_WhenAudioPodcastAwaitingYouTubeAndDelayNotYetDue_ReturnsTrue()
    {
        // Arrange
        var podcast = new Podcast
        {
            SpotifyId = "spotify",
            YouTubeChannelId = "channel",
            YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks
        };
        var episode = new Episode
        {
            ReleaseUtc = DateTime.UtcNow.AddMinutes(-30),
            Length = TimeSpan.FromHours(2),
            Urls = new ServiceUrls()
        };

        // Act
        var result = podcast.IsDelayedYouTubePublishing(episode);

        // Assert
        result.Should().BeTrue();
    }
}
