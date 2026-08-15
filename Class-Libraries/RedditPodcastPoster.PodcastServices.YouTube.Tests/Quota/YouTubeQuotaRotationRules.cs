using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Quota;

public class YouTubeQuotaRotationRules
{
    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeVideoService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_Rotates_And_Retries()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        
        var mockBaseService = new Mock<IYouTubeVideoService>();
        
        // First call throws quota exception, second call succeeds
        mockBaseService.SetupSequence(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException())
            .ReturnsAsync(new List<Google.Apis.YouTube.v3.Data.Video>());

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new TolerantYouTubeVideoService(
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubeVideoService>.Instance);
        
        var indexingContext = new IndexingContext();
        var videoIds = new[] { "video-id" };

        // Act
        var result = await sut.GetVideoContentDetails(mockWrapper.Object, videoIds, indexingContext);

        // Assert
        result.Should().NotBeNull();
        mockWrapper.Verify(x => x.Rotate(), Times.Once, "Rotate should be called after a quota exception");
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse("Quota is not exhausted if rotation succeeded");
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception and rotation fails, TolerantYouTubeVideoService marks quota as exhausted but does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_And_Rotation_Fails_Marks_Quota_Exhausted()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.Setup(x => x.Rotate()).Throws(new Exception("Ring exhausted"));
        
        var mockBaseService = new Mock<IYouTubeVideoService>();
        mockBaseService.Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new TolerantYouTubeVideoService(
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubeVideoService>.Instance);
        
        var indexingContext = new IndexingContext();
        var videoIds = new[] { "video-id" };

        // Act
        var result = await sut.GetVideoContentDetails(mockWrapper.Object, videoIds, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.YouTubeQuotaExhausted.Should().BeTrue("Quota IS exhausted if rotation fails");
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("SkipYouTubeUrlResolving should not be set by quota issues anymore");
    }
}
