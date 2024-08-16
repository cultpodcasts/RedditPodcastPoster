using FluentAssertions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubePodcastServiceMatcherTests
{
    [Fact]
    public void IsMatch_WithShortnerUrl_IsCorrect()
    {
        // arrange
        var url = new Uri("https://youtu.be/AB-CDEFG123");
        // act
        var result = YouTubePodcastServiceMatcher.IsMatch(url);
        // assert
        result.Should().BeTrue();
    }
}