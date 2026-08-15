using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using System.Threading;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Quota;

public class YouTubeQuotaRotationRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Application _app1;
    private readonly Application _app2;
    private readonly string _apiKey1;
    private readonly string _apiKey2;
    private Application _currentApp;

    public YouTubeQuotaRotationRules()
    {
        _apiKey1 = _fixture.Create<string>();
        _apiKey2 = _fixture.Create<string>();
        _app1 = _fixture.Build<Application>()
            .With(x => x.ApiKey, _apiKey1)
            .With(x => x.Usage, ApplicationUsage.Indexer)
            .Create();
        _app2 = _fixture.Build<Application>()
            .With(x => x.ApiKey, _apiKey2)
            .With(x => x.Usage, ApplicationUsage.Indexer)
            .Create();
        _currentApp = _app1;

        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => _currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => _currentApp = _app2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Setup(x => x.RecordQuotaHitAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<YouTubeQuotaOperation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeVideoService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_Rotates_And_Retries()
    {
        // Arrange
        var seenApps = new List<Application>();
        _mocker.GetMock<IYouTubeVideoService>().Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IYouTubeServiceWrapper w, IEnumerable<string> ids, IndexingContext ic, bool b1, bool b2, bool b3) =>
            {
                seenApps.Add(w.CurrentApplication);
                if (seenApps.Count == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new List<Google.Apis.YouTube.v3.Data.Video>();
            });

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeVideoService>>(NullLogger<TolerantYouTubeVideoService>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubeVideoService>();
        
        var indexingContext = new IndexingContext();
        var videoIds = _fixture.Create<string[]>();

        // Act
        var result = await sut.GetVideoContentDetails(_mocker.GetMock<IYouTubeServiceWrapper>().Object, videoIds, indexingContext);

        // Assert
        result.Should().NotBeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        seenApps.Should().HaveCount(2);
        seenApps[0].ApiKey.Should().Be(_apiKey1);
        seenApps[1].ApiKey.Should().Be(_apiKey2);
        
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(
            _app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.VideosList,
            It.IsAny<CancellationToken>()), Times.Once);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception and rotation fails, TolerantYouTubeVideoService marks quota as exhausted but does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_And_Rotation_Fails_Marks_Quota_Exhausted()
    {
        // Arrange
        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Throws(new Exception(_fixture.Create<string>()));
        
        _mocker.GetMock<IYouTubeVideoService>().Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeVideoService>>(NullLogger<TolerantYouTubeVideoService>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubeVideoService>();
        
        var indexingContext = new IndexingContext();
        var videoIds = _fixture.Create<string[]>();

        // Act
        var result = await sut.GetVideoContentDetails(_mocker.GetMock<IYouTubeServiceWrapper>().Object, videoIds, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.YouTubeQuotaExhausted.Should().BeTrue();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubePlaylistService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubePlaylistService_Rotates_And_Retries()
    {
        // Arrange
        var seenApps = new List<Application>();
        _mocker.GetMock<IYouTubePlaylistService>().Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync((IYouTubeServiceWrapper w, YouTubePlaylistId id, IndexingContext ic, bool b1, bool b2, PlaylistOrder? po) =>
            {
                seenApps.Add(w.CurrentApplication);
                if (seenApps.Count == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new GetPlaylistVideoSnippetsResponse(new List<Google.Apis.YouTube.v3.Data.PlaylistItem>());
            });

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubePlaylistService>>(NullLogger<TolerantYouTubePlaylistService>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubePlaylistService>();
        
        var indexingContext = new IndexingContext();
        var playlistId = _fixture.Create<YouTubePlaylistId>();

        // Act
        var result = await sut.GetPlaylistVideoSnippets(playlistId, indexingContext);

        // Assert
        result.Result.Should().NotBeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        seenApps.Should().HaveCount(2);
        seenApps[0].ApiKey.Should().Be(_apiKey1);
        seenApps[1].ApiKey.Should().Be(_apiKey2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(
            _app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.PlaylistItemsList,
            It.IsAny<CancellationToken>()), Times.Once);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeSearcher rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeSearcher_Rotates_And_Retries()
    {
        // Arrange
        var callCount = 0;
        _mocker.GetMock<IYouTubeSearcher>().Setup(x => x.Search(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new List<RedditPodcastPoster.PodcastServices.Abstractions.Models.EpisodeResult>();
            });

        var seenRecordCallApps = new List<Application>();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, CancellationToken>((app, usage, ct) => seenRecordCallApps.Add(app))
            .Returns(Task.CompletedTask);

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeSearcher>>(NullLogger<TolerantYouTubeSearcher>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubeSearcher>();
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.Search(_fixture.Create<string>(), indexingContext);

        // Assert
        result.Should().NotBeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        callCount.Should().Be(2);
        seenRecordCallApps.Should().HaveCount(2);
        seenRecordCallApps[0].ApiKey.Should().Be(_apiKey1);
        seenRecordCallApps[1].ApiKey.Should().Be(_apiKey2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(
            _app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeChannelResolver rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeChannelResolver_Rotates_And_Retries()
    {
        // Arrange
        var callCount = 0;
        _mocker.GetMock<IYouTubeChannelResolver>().Setup(x => x.FindChannelsSnippets(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new Google.Apis.YouTube.v3.Data.SearchResult();
            });

        var seenRecordCallApps = new List<Application>();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, CancellationToken>((app, usage, ct) => seenRecordCallApps.Add(app))
            .Returns(Task.CompletedTask);

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeChannelResolver>>(NullLogger<TolerantYouTubeChannelResolver>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelResolver>();
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.FindChannelsSnippets(_fixture.Create<string>(), _fixture.Create<string>(), indexingContext);

        // Assert
        result.Should().NotBeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        callCount.Should().Be(2);
        seenRecordCallApps.Should().HaveCount(2);
        seenRecordCallApps[0].ApiKey.Should().Be(_apiKey1);
        seenRecordCallApps[1].ApiKey.Should().Be(_apiKey2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(
            _app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeChannelVideoSnippetsService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeChannelVideoSnippetsService_Rotates_And_Retries()
    {
        // Arrange
        var seenApps = new List<Application>();
        _mocker.GetMock<IYouTubeChannelVideoSnippetsService>().Setup(x => x.GetLatestChannelVideoSnippets(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync((IYouTubeServiceWrapper w, YouTubeChannelId id, IndexingContext ic) =>
            {
                seenApps.Add(w.CurrentApplication);
                if (seenApps.Count == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new List<Google.Apis.YouTube.v3.Data.SearchResult>();
            });

        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubeChannelVideoSnippetsService>>(NullLogger<TolerantYouTubeChannelVideoSnippetsService>.Instance);
        var sut = _mocker.CreateInstance<TolerantYouTubeChannelVideoSnippetsService>();
        
        var indexingContext = new IndexingContext();
        var channelId = _fixture.Create<YouTubeChannelId>();

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(channelId, indexingContext);

        // Assert
        result.Should().NotBeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().Verify(x => x.Rotate(), Times.Once);
        seenApps.Should().HaveCount(2);
        seenApps[0].ApiKey.Should().Be(_apiKey1);
        seenApps[1].ApiKey.Should().Be(_apiKey2);

        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(
            _app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }
}
