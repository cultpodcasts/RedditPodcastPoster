using FluentAssertions;
using Google;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Channel;

public class YouTubeChannelServiceRules
{
    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetChannel returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw, triggering the catch(Exception) block
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubeChannelService(mockWrapper.Object, quotaTracker.Object, NullLogger<YouTubeChannelService>.Instance);
        
        var indexingContext = new IndexingContext();
        var channelId = new YouTubeChannelId("channel-id");

        // Act
        var result = await sut.GetChannel(channelId, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }
}
