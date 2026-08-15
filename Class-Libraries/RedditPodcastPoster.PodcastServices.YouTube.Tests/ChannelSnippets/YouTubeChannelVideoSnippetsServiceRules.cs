using FluentAssertions;
using Google;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using System.Net;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.ChannelSnippets;

public class YouTubeChannelVideoSnippetsServiceRules
{
    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetLatestChannelVideoSnippets returns partial results and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubeChannelVideoSnippetsService(quotaTracker.Object, NullLogger<YouTubeChannelVideoSnippetsService>.Instance);
        
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw, triggering the catch(Exception) block
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var indexingContext = new IndexingContext();
        var channelId = new YouTubeChannelId("channel-id");

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(mockWrapper.Object, channelId, indexingContext);

        // Assert
        result.Should().BeEmpty();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }
}
