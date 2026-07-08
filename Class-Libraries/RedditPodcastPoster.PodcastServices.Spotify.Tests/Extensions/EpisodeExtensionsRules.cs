using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Extensions;

/// <summary>
/// Spotify SDK types Images and ExternalUrls as non-empty collections, but the API can omit them.
/// </summary>
public class EpisodeExtensionsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When SimpleEpisode has no images, GetBestImageUrl returns null " +
        "because missing artwork must not throw during catalogue mapping.")]
    public void Simple_episode_without_images_returns_null_image_url()
    {
        // Arrange
        var episode = new SimpleEpisode
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            Images = []
        };

        // Act
        var result = episode.GetBestImageUrl();

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When FullEpisode has no images, GetBestImageUrl returns null " +
        "because enricher thumbnail backfill must tolerate absent artwork.")]
    public void Full_episode_without_images_returns_null_image_url()
    {
        // Arrange
        var episode = new FullEpisode
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            Images = []
        };

        // Act
        var result = episode.GetBestImageUrl();

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When SimpleEpisode has multiple images, GetBestImageUrl picks the tallest image " +
        "because indexed artwork should prefer the highest-resolution asset.")]
    public void Simple_episode_picks_tallest_image()
    {
        // Arrange
        var lowRes = new Uri("https://example.com/low.jpg");
        var highRes = new Uri("https://example.com/high.jpg");
        var episode = new SimpleEpisode
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            Images =
            [
                new Image { Url = lowRes.ToString(), Height = 64 },
                new Image { Url = highRes.ToString(), Height = 640 }
            ]
        };

        // Act
        var result = episode.GetBestImageUrl();

        // Assert
        result.Should().Be(highRes);
    }

    [Fact(DisplayName =
        "When FullEpisode ExternalUrls contains a Spotify link, GetUrl returns that URI " +
        "because catalogue mapping must resolve episode links from the first external URL entry.")]
    public void Full_episode_get_url_uses_first_external_url()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var episode = new FullEpisode
        {
            Id = spotifyId,
            Name = _fixture.CreateTitle(),
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = spotifyUrl.ToString() }
        };

        // Act
        var result = episode.GetUrl();

        // Assert
        result.Should().Be(spotifyUrl);
    }
}
