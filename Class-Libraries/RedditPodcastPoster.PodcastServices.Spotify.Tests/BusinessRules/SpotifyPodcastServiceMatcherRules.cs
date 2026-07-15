using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules;

/// <summary>
/// Submit/API host matching must treat any host containing "spotify" as Spotify (case-insensitive).
/// </summary>
public class SpotifyPodcastServiceMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "When the URL host contains 'spotify' (any casing), IsMatch returns true " +
        "because submit routing must recognize Spotify episode and show hosts.")]
    [InlineData("https://open.spotify.com/episode/abc")]
    [InlineData("https://OPEN.SPOTIFY.COM/episode/abc")]
    [InlineData("https://spotify.link/shortcode")]
    public void Spotify_host_is_match(string url)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var result = SpotifyPodcastServiceMatcher.IsMatch(uri);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the URL host is not a Spotify host, IsMatch returns false " +
        "because non-Spotify submit URLs must fall through to other categorisers.")]
    public void Non_spotify_host_is_not_match()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var uri = new Uri($"https://www.youtube.com/watch?v={youTubeId}");

        // Act
        var result = SpotifyPodcastServiceMatcher.IsMatch(uri);

        // Assert
        result.Should().BeFalse();
    }
}
