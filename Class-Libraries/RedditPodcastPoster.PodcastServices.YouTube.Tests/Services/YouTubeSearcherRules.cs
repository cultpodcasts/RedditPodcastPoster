using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class YouTubeSearcherRules
{
    [Fact(DisplayName = "When YouTube API throws an exception during search, Search returns partial results and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Exception_During_Search_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var httpClientFactory = new Mock<INoRedirectHttpClientFactory>();
        var videoService = new Mock<ITolerantYouTubeVideoService>();
        var channelService = new Mock<ITolerantYouTubeChannelService>();
        var thumbnailResolver = new Mock<IYouTubeThumbnailResolver>();

        var sut = new YouTubeSearcher(
            mockWrapper.Object, 
            httpClientFactory.Object, 
            videoService.Object, 
            channelService.Object, 
            thumbnailResolver.Object, 
            NullLogger<YouTubeSearcher>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.Search("query", indexingContext);

        // Assert
        result.Should().BeEmpty();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }
}
