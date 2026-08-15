using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeVideoService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_Rotates_And_Retries()
    {
        // Arrange
        var app1 = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var app2 = new Application
        {
            ApiKey = "key2",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-2"
        };

        var currentApp = app1;
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => currentApp = app2);
        
        var mockBaseService = new Mock<IYouTubeVideoService>();
        var seenApps = new List<Application>();
        
        mockBaseService.Setup(x => x.GetVideoContentDetails(
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
        seenApps.Should().HaveCount(2, "The service should retry the call after rotation");
        seenApps[0].ApiKey.Should().Be("key1", "First call should use the initial API key");
        seenApps[1].ApiKey.Should().Be("key2", "Retry should use the rotated API key");
        
        quotaTracker.Verify(x => x.RecordQuotaHitAsync(
            app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.VideosList,
            It.IsAny<CancellationToken>()), Times.Once, "Quota hit should be recorded for the first application");
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

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubePlaylistService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubePlaylistService_Rotates_And_Retries()
    {
        // Arrange
        var app1 = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var app2 = new Application
        {
            ApiKey = "key2",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-2"
        };

        var currentApp = app1;
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => currentApp = app2);
        
        var mockBaseService = new Mock<IYouTubePlaylistService>();
        var seenApps = new List<Application>();
        
        mockBaseService.Setup(x => x.GetPlaylistVideoSnippets(
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

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new TolerantYouTubePlaylistService(
            mockWrapper.Object,
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubePlaylistService>.Instance);
        
        var indexingContext = new IndexingContext();
        var playlistId = new YouTubePlaylistId("playlist-id");

        // Act
        var result = await sut.GetPlaylistVideoSnippets(playlistId, indexingContext);

        // Assert
        result.Result.Should().NotBeNull();
        mockWrapper.Verify(x => x.Rotate(), Times.Once);
        seenApps.Should().HaveCount(2, "The service should retry the call after rotation");
        seenApps[0].ApiKey.Should().Be("key1");
        seenApps[1].ApiKey.Should().Be("key2");

        quotaTracker.Verify(x => x.RecordQuotaHitAsync(
            app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.PlaylistItemsList,
            It.IsAny<CancellationToken>()), Times.Once, "Quota hit should be recorded");
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeSearcher rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeSearcher_Rotates_And_Retries()
    {
        // Arrange
        var app1 = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var app2 = new Application
        {
            ApiKey = "key2",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-2"
        };

        var currentApp = app1;
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => currentApp = app2);
        
        var mockBaseService = new Mock<IYouTubeSearcher>();
        var callCount = 0;
        
        mockBaseService.Setup(x => x.Search(
                It.IsAny<string>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new List<RedditPodcastPoster.PodcastServices.Abstractions.Models.EpisodeResult>();
            });

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var seenRecordCallApps = new List<Application>();
        quotaTracker.Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, CancellationToken>((app, usage, ct) => seenRecordCallApps.Add(app))
            .Returns(Task.CompletedTask);

        var sut = new TolerantYouTubeSearcher(
            mockWrapper.Object,
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubeSearcher>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.Search("query", indexingContext);

        // Assert
        result.Should().NotBeNull();
        mockWrapper.Verify(x => x.Rotate(), Times.Once);
        callCount.Should().Be(2, "The service should retry the search after rotation");
        seenRecordCallApps.Should().HaveCount(2);
        seenRecordCallApps[0].ApiKey.Should().Be("key1");
        seenRecordCallApps[1].ApiKey.Should().Be("key2");

        quotaTracker.Verify(x => x.RecordQuotaHitAsync(
            app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once, "Quota hit should be recorded");
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeChannelResolver rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeChannelResolver_Rotates_And_Retries()
    {
        // Arrange
        var app1 = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var app2 = new Application
        {
            ApiKey = "key2",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-2"
        };

        var currentApp = app1;
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => currentApp = app2);
        
        var mockBaseService = new Mock<IYouTubeChannelResolver>();
        var callCount = 0;
        
        mockBaseService.Setup(x => x.FindChannelsSnippets(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YouTubeQuotaException();
                }
                return new Google.Apis.YouTube.v3.Data.SearchResult();
            });

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var seenRecordCallApps = new List<Application>();
        quotaTracker.Setup(x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()))
            .Callback<Application, ApplicationUsage, CancellationToken>((app, usage, ct) => seenRecordCallApps.Add(app))
            .Returns(Task.CompletedTask);

        var sut = new TolerantYouTubeChannelResolver(
            mockWrapper.Object,
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubeChannelResolver>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.FindChannelsSnippets("channel", "video", indexingContext);

        // Assert
        result.Should().NotBeNull();
        mockWrapper.Verify(x => x.Rotate(), Times.Once);
        callCount.Should().Be(2, "The service should retry the channel resolution after rotation");
        seenRecordCallApps.Should().HaveCount(2);
        seenRecordCallApps[0].ApiKey.Should().Be("key1");
        seenRecordCallApps[1].ApiKey.Should().Be("key2");

        quotaTracker.Verify(x => x.RecordQuotaHitAsync(
            app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once, "Quota hit should be recorded");
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, TolerantYouTubeChannelVideoSnippetsService rotates the API key and tries again")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_TolerantYouTubeChannelVideoSnippetsService_Rotates_And_Retries()
    {
        // Arrange
        var app1 = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-1"
        };

        var app2 = new Application
        {
            ApiKey = "key2",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Primary-2"
        };

        var currentApp = app1;
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.CanRotate).Returns(true);
        mockWrapper.SetupGet(x => x.Usage).Returns(ApplicationUsage.Indexer);
        mockWrapper.SetupGet(x => x.CurrentApplication).Returns(() => currentApp);
        mockWrapper.Setup(x => x.Rotate()).Callback(() => currentApp = app2);
        
        var mockBaseService = new Mock<IYouTubeChannelVideoSnippetsService>();
        var seenApps = new List<Application>();
        
        mockBaseService.Setup(x => x.GetLatestChannelVideoSnippets(
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

        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new TolerantYouTubeChannelVideoSnippetsService(
            mockWrapper.Object,
            mockBaseService.Object, 
            quotaTracker.Object, 
            NullLogger<TolerantYouTubeChannelVideoSnippetsService>.Instance);
        
        var indexingContext = new IndexingContext();
        var channelId = new YouTubeChannelId("channel-id");

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(channelId, indexingContext);

        // Assert
        result.Should().NotBeNull();
        mockWrapper.Verify(x => x.Rotate(), Times.Once);
        seenApps.Should().HaveCount(2, "The service should retry the call after rotation");
        seenApps[0].ApiKey.Should().Be("key1");
        seenApps[1].ApiKey.Should().Be("key2");

        quotaTracker.Verify(x => x.RecordQuotaHitAsync(
            app1,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            It.IsAny<CancellationToken>()), Times.Once, "Quota hit should be recorded");
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
    }
}
