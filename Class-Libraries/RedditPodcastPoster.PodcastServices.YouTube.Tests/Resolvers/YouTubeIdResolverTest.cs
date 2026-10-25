using FluentAssertions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

public class YouTubeIdResolverTest
{
    [Fact(DisplayName =
        "Extract returns the video id from a youtu.be short URL, so catalog and share paths share one parser.")]
    public void Extract_WithShortnerUrl_IsCorrect()
    {
        // Arrange
        var expected = "AB-CDEFG123";
        var url = new Uri("https://youtu.be/" + expected);

        // Act
        var result = YouTubeIdResolver.Extract(url);

        // Assert
        result.Should().Be(expected);
    }
}