using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class TolerantYouTubeChannelServiceTests
{
    [Fact]
    public async Task GetChannel_OnQuotaException_RotatesAndRetries()
    {
        var channelId = new YouTubeChannelId("channel-1");
        var indexingContext = new IndexingContext();
        var channel = new Google.Apis.YouTube.v3.Data.Channel { Id = channelId.ChannelId };
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var wrapper = new Mock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.CanRotate).Returns(true);
        wrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        wrapper.SetupGet(x => x.CurrentApplication).Returns(application);

        var callCount = 0;
        var channelService = new Mock<IYouTubeChannelService>();
        channelService
            .Setup(x => x.GetChannel(
                channelId,
                indexingContext,
                true,
                false,
                false,
                false))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }

                return channel;
            });

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        quotaTracker
            .Setup(x => x.RecordCallAsync(It.IsAny<Application>(), ApplicationUsage.Indexer, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        quotaTracker
            .Setup(x => x.RecordQuotaHitAsync(
                It.IsAny<Application>(),
                ApplicationUsage.Indexer,
                It.IsAny<YouTubeQuotaOperation>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new TolerantYouTubeChannelService(
            wrapper.Object,
            channelService.Object,
            quotaTracker.Object,
            NullLogger<TolerantYouTubeChannelService>.Instance);

        var result = await sut.GetChannel(channelId, indexingContext, withSnippets: true);

        result.Should().Be(channel);
        wrapper.Verify(x => x.Rotate(), Times.Once);
        channelService.Verify(
            x => x.GetChannel(channelId, indexingContext, true, false, false, false),
            Times.Exactly(2));
        quotaTracker.Verify(
            x => x.RecordQuotaHitAsync(
                application,
                ApplicationUsage.Indexer,
                YouTubeQuotaOperation.ChannelsList,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetChannel_WhenRotationExhausted_SetsSkipYouTubeUrlResolving()
    {
        var channelId = new YouTubeChannelId("channel-1");
        var indexingContext = new IndexingContext();
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var wrapper = new Mock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.CanRotate).Returns(true);
        wrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        wrapper.SetupGet(x => x.CurrentApplication).Returns(application);
        wrapper.Setup(x => x.Rotate()).Throws(new InvalidOperationException("Indexer key ring exhausted."));

        var channelService = new Mock<IYouTubeChannelService>();
        channelService
            .Setup(x => x.GetChannel(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        quotaTracker
            .Setup(x => x.RecordCallAsync(It.IsAny<Application>(), ApplicationUsage.Indexer, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        quotaTracker
            .Setup(x => x.RecordQuotaHitAsync(
                It.IsAny<Application>(),
                ApplicationUsage.Indexer,
                It.IsAny<YouTubeQuotaOperation>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new TolerantYouTubeChannelService(
            wrapper.Object,
            channelService.Object,
            quotaTracker.Object,
            NullLogger<TolerantYouTubeChannelService>.Instance);

        var result = await sut.GetChannel(channelId, indexingContext);

        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeTrue();
    }
}
