using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// A channel-only podcast fetched on a pass where YouTube is bypassed must not be reported as a missing channel:
/// nothing was looked up, so nothing is missing.
/// </summary>
public class YouTubeChannelVideosSkippedRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeChannelVideosService> _logger = new();

    [Fact(DisplayName = "When YouTube is bypassed, a channel-only fetch is not reported as not found")]
    public async Task Bypassed_Pass_Does_Not_Report_NotFound()
    {
        // Arrange
        // Real channel services over a mocked API wrapper, so the bypass flows through the real code path.
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CanRotate).Returns(true);
        _mocker.Use<IYouTubeChannelService>(_mocker.CreateInstance<YouTubeChannelService>());
        _mocker.Use<ITolerantYouTubeChannelService>(_mocker.CreateInstance<TolerantYouTubeChannelService>());
        _mocker.Use<ILogger<YouTubeChannelVideosService>>(_logger);
        var sut = _mocker.CreateInstance<YouTubeChannelVideosService>();
        var channelId = new YouTubeChannelId(_fixture.CreateYouTubeChannelId());
        var indexingContext = new IndexingContext(SkipYouTubeUrlResolving: true);

        // Act
        var response = await sut.GetChannelVideos(channelId, indexingContext);

        // Assert
        response.PlaylistItems.Should().BeNull();
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.Skipped);
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
        _mocker.GetMock<ITolerantYouTubePlaylistService>().VerifyNoOtherCalls();
    }
}
