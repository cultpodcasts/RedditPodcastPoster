using FluentAssertions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

public class YouTubeIdResolverTest
{
    [Fact]
    public void IsMatch_WithShortnerUrl_IsCorrect()
    {
        // arrange
        var expected = "AB-CDEFG123";
        var url = new Uri("https://youtu.be/" + expected);
        // act
        var result = YouTubeIdResolver.Extract(url);
        // assert
        result.Should().Be(expected);
    }
}