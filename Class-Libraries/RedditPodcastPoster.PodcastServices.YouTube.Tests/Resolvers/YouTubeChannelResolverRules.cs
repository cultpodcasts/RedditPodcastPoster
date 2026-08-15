using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

public class YouTubeChannelResolverRules
{
    [Fact(DisplayName = "When YouTube API throws an exception during channel search, FindChannelsSnippets returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Exception_During_Search_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var sut = new YouTubeChannelResolver(mockWrapper.Object, NullLogger<YouTubeChannelResolver>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.FindChannelsSnippets("channel-name", "video-title", indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }
}
