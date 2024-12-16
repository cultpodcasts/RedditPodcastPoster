using FluentAssertions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Extensions
{
    public class SpotifyUriExtensionsTests
    {
        [Fact]
        public void CleanSpotifyUrl_WithoutQueryString_IsCorrect()
        {
            // arrange
            var expected = "https://open.spotify.com/episode/01234567890ABCDEF";
            var url = new Uri(expected);
            // act
            var result = url.CleanSpotifyUrl();
            // assert
            result.ToString().Should().Be(expected);
        }

        [Fact]
        public void CleanSpotifyUrl_WithQueryString_IsCorrect()
        {
            // arrange
            var expected = "https://open.spotify.com/episode/01234567890ABCDEF";
            var url = new Uri(expected + "?si=11516382cd81494d");
            // act
            var result = url.CleanSpotifyUrl();
            // assert
            result.ToString().Should().Be(expected);
        }
    }
}
