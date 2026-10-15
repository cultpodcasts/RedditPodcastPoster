using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class TolerantYouTubeChannelServiceTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Application _app1;
    private readonly Application _app2;
    private Application _currentApp;

    public TolerantYouTubeChannelServiceTests()
    {
        _app1 = _fixture.Build<Application>()
            .With(x => x.ApiKey, "key1")
            .With(x => x.Usage, ApplicationUsage.Indexer)
            .Create();
        _app2 = _fixture.Build<Application>()
            .With(x => x.ApiKey, "key2")
            .With(x => x.Usage, ApplicationUsage.Indexer)
            .Create();
        _currentApp = _app1;

        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CanRotate).Returns(true);
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CurrentApplication).Returns(() => _currentApp);
        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Callback(() => _currentApp = _app2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordQuotaHitAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<YouTubeQuotaOperation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeChannelService>>(NullLogger<TolerantYouTubeChannelService>.Instance);
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, GetChannel rotates and retries")]
    public async Task GetChannel_OnQuotaException_RotatesAndRetries()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var indexingContext = new IndexingContext();
        var channel = new Google.Apis.YouTube.v3.Data.Channel { Id = channelId.ChannelId };

        var callCount = 0;
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(channelId, indexingContext, true, false, false, false))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }

                return YouTubeChannelLookupResult.Found(channel);
            });

        var seenRecordCallApps = new List<Application>();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordCallAsync(It.IsAny<Application>(), ApplicationUsage.Indexer, It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, CancellationToken>((app, usage, ct) => seenRecordCallApps.Add(app))
            .Returns(Task.CompletedTask);

        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        var result = await sut.GetChannel(channelId, indexingContext, withSnippets: true);

        // Assert
        result.Should().Be(channel);
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        _mocker.GetMock<IYouTubeChannelService>().Verify(
            x => x.GetChannelLookup(channelId, indexingContext, true, false, false, false),
            Times.Exactly(2));

        seenRecordCallApps.Should().HaveCount(2);
        seenRecordCallApps[0].ApiKey.Should().Be("key1");
        seenRecordCallApps[1].ApiKey.Should().Be("key2");

        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(
            x => x.RecordQuotaHitAsync(
                _app1,
                ApplicationUsage.Indexer,
                YouTubeQuotaOperation.ChannelsList,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "When rotation is exhausted, GetChannel returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task GetChannel_WhenRotationExhausted_Does_Not_Set_SkipYouTubeUrlResolving()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var indexingContext = new IndexingContext();

        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Throws(new InvalidOperationException("Ring exhausted"));

        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());

        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        var result = await sut.GetChannel(channelId, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When rotation is exhausted, GetChannelLookup reports QuotaExhausted")]
    public async Task GetChannelLookup_WhenRotationExhausted_Reports_QuotaExhausted()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Throws(new InvalidOperationException("Ring exhausted"));
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        var result = await sut.GetChannelLookup(channelId, new IndexingContext());

        // Assert
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.QuotaExhausted);
        result.Channel.Should().BeNull();
    }

    [Fact(DisplayName = "When YouTube is bypassed and the channel is not cached, GetChannelLookup reports Skipped and records no API call")]
    public async Task GetChannelLookup_WhenBypassed_Reports_Skipped()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var indexingContext = new IndexingContext(SkipYouTubeUrlResolving: true);
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(channelId, indexingContext, false, false, false, false))
            .ReturnsAsync(YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.Skipped));
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        var result = await sut.GetChannelLookup(channelId, indexingContext);

        // Assert
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.Skipped);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(
            x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Never);
    }

    [Fact(DisplayName = "When YouTube is bypassed but the channel is in the pass cache, GetChannelLookup reports Found and GetChannel returns it")]
    public async Task GetChannelLookup_WhenBypassed_And_Cached_Reports_Found()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var indexingContext = new IndexingContext(SkipYouTubeUrlResolving: true);
        var channel = new Google.Apis.YouTube.v3.Data.Channel { Id = channelId.ChannelId };
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(channelId, indexingContext, false, false, false, false))
            .ReturnsAsync(YouTubeChannelLookupResult.Found(channel));
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        var lookup = await sut.GetChannelLookup(channelId, indexingContext);
        var returned = await sut.GetChannel(channelId, indexingContext);

        // Assert
        lookup.Outcome.Should().Be(YouTubeChannelLookupOutcome.Found);
        lookup.Channel.Should().BeSameAs(channel);
        returned.Should().BeSameAs(channel);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(
            x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "When rotation is exhausted, GetChannelLookup marks YouTube quota exhausted and does not set the skip flag")]
    public async Task GetChannelLookup_WhenRotationExhausted_Marks_Quota_Not_Skip()
    {
        // Arrange
        var indexingContext = new IndexingContext();
        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Throws(new InvalidOperationException("Ring exhausted"));
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelService>();

        // Act
        await sut.GetChannelLookup(_fixture.Create<YouTubeChannelId>(), indexingContext);

        // Assert
        indexingContext.YouTubeQuotaExhausted.Should().BeTrue();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }
}