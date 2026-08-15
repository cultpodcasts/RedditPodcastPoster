using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Video;

public class YouTubeVideoServiceRules
{
    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetVideoContentDetails returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubeVideoService(quotaTracker.Object, NullLogger<YouTubeVideoService>.Instance);
        
        var indexingContext = new IndexingContext();
        var videoIds = new[] { "video-id" };

        // Act
        var result = await sut.GetVideoContentDetails(mockWrapper.Object, videoIds, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }
}
