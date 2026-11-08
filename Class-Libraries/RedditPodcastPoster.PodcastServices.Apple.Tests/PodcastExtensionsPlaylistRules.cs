using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

/// <summary>
/// A YouTube playlist is a YouTube source on its own (RPP #1177): a podcast with a playlist and no channel id is
/// discovered from YouTube, so it joins the YouTube discovery pool.
/// </summary>
public class PodcastExtensionsPlaylistRules
{
    [Fact(DisplayName =
        "A podcast with a YouTube playlist and no channel id depends on YouTube for discovery, because the playlist " +
        "is a YouTube source on its own.")]
    public void playlist_only_depends_on_youtube()
    {
        // Arrange
        var podcast = new Podcast
        {
            YouTubeChannelId = string.Empty,
            YouTubePlaylistId = "PLsyntheticplaylist0000000000000000"
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A podcast with neither a YouTube channel id nor a playlist does not depend on YouTube for discovery.")]
    public void no_youtube_source_does_not_depend_on_youtube()
    {
        // Arrange
        var podcast = new Podcast
        {
            YouTubeChannelId = string.Empty,
            YouTubePlaylistId = string.Empty
        };

        // Act
        var result = podcast.DependsOnYouTubeForEpisodeDiscovery();

        // Assert
        result.Should().BeFalse();
    }
}
