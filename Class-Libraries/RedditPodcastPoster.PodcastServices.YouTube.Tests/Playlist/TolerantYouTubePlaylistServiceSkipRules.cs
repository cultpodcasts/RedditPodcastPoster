using AutoFixture;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

public class TolerantYouTubePlaylistServiceSkipRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TolerantYouTubePlaylistServiceSkipRules()
    {
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CanRotate).Returns(true);
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<TolerantYouTubePlaylistService>>(
            NullLogger<TolerantYouTubePlaylistService>.Instance);
    }

    [Fact(DisplayName = "When the playlist fetch is skipped because YouTube is bypassed, no quota call is recorded")]
    public async Task Skipped_Fetch_Records_No_Quota_Call()
    {
        // Arrange
        var playlistId = _fixture.Create<YouTubePlaylistId>();
        var indexingContext = new IndexingContext(SkipYouTubeUrlResolving: true);
        _mocker.GetMock<IYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<IYouTubeServiceWrapper>(),
                playlistId,
                indexingContext,
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(null, Failure: YouTubePlaylistFetchFailure.Skipped));
        var sut = _mocker.CreateInstance<TolerantYouTubePlaylistService>();

        // Act
        var response = await sut.GetPlaylistVideoSnippets(playlistId, indexingContext);

        // Assert
        response.Result.Should().BeNull();
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.Skipped);
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(
            x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "When the playlist fetch makes a real API call, one quota call is recorded")]
    public async Task Real_Fetch_Records_One_Quota_Call()
    {
        // Arrange
        var playlistId = _fixture.Create<YouTubePlaylistId>();
        var indexingContext = new IndexingContext();
        IList<PlaylistItem> items = [new PlaylistItem { Id = _fixture.Create<string>() }];
        _mocker.GetMock<IYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<IYouTubeServiceWrapper>(),
                playlistId,
                indexingContext,
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(items));
        var sut = _mocker.CreateInstance<TolerantYouTubePlaylistService>();

        // Act
        var response = await sut.GetPlaylistVideoSnippets(playlistId, indexingContext);

        // Assert
        response.Result.Should().BeSameAs(items);
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(
            x => x.RecordCallAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
