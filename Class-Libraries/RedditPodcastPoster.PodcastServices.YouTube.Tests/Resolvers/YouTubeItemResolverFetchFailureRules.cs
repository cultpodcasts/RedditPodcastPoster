using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

/// <summary>
/// The item resolver reports a playlist fetch failure only when one really happened: a skipped or
/// quota-exhausted fetch logs nothing, a NotFound or ApiError still logs an error. This holds for a stored
/// playlist id and for a channel-only podcast that falls back to the uploads playlist.
/// </summary>
public class YouTubeItemResolverFetchFailureRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeItemResolver> _logger = new();

    [Theory(DisplayName = "When a stored-playlist fetch is skipped or quota-exhausted, the resolver logs no fetch failure")]
    [InlineData(YouTubePlaylistFetchFailure.Skipped)]
    [InlineData(YouTubePlaylistFetchFailure.QuotaExhausted)]
    public async Task Stored_Playlist_Skipped_Or_Quota_Logs_No_Error(YouTubePlaylistFetchFailure failure)
    {
        // Arrange
        var podcast = CreatePodcast(withPlaylist: true);
        var sut = CreateSut(playlistFailure: failure);

        // Act
        var response = await sut.FindEpisode(CreateRequest(podcast), new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Should().BeNull();
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("was not found"));
    }

    [Theory(DisplayName = "When a stored-playlist fetch really failed, the resolver still logs an error")]
    [InlineData(YouTubePlaylistFetchFailure.NotFound, "was not found")]
    [InlineData(YouTubePlaylistFetchFailure.ApiError, "playlist fetch failed")]
    public async Task Stored_Playlist_Real_Failure_Logs_Error(YouTubePlaylistFetchFailure failure, string expectedPhrase)
    {
        // Arrange
        var podcast = CreatePodcast(withPlaylist: true);
        var sut = CreateSut(playlistFailure: failure);

        // Act
        await sut.FindEpisode(CreateRequest(podcast), new IndexingContext());

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(expectedPhrase));
    }

    [Theory(DisplayName = "When a channel-uploads fetch is skipped or quota-exhausted, the resolver logs no fetch failure")]
    [InlineData(YouTubePlaylistFetchFailure.Skipped)]
    [InlineData(YouTubePlaylistFetchFailure.QuotaExhausted)]
    public async Task Channel_Uploads_Skipped_Or_Quota_Logs_No_Error(YouTubePlaylistFetchFailure failure)
    {
        // Arrange
        var podcast = CreatePodcast(withPlaylist: false);
        var sut = CreateSut(channelVideosFailure: failure);

        // Act
        var response = await sut.FindEpisode(CreateRequest(podcast), new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Should().BeNull();
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("was not found"));
    }

    [Theory(DisplayName = "When a channel-uploads fetch really failed, the resolver still logs an error")]
    [InlineData(YouTubePlaylistFetchFailure.NotFound, "was not found")]
    [InlineData(YouTubePlaylistFetchFailure.ApiError, "playlist fetch failed")]
    public async Task Channel_Uploads_Real_Failure_Logs_Error(YouTubePlaylistFetchFailure failure, string expectedPhrase)
    {
        // Arrange
        var podcast = CreatePodcast(withPlaylist: false);
        var sut = CreateSut(channelVideosFailure: failure);

        // Act
        await sut.FindEpisode(CreateRequest(podcast), new IndexingContext());

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(expectedPhrase));
    }

    private Podcast CreatePodcast(bool withPlaylist) =>
        _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            if (withPlaylist)
            {
                p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            }
        });

    private EnrichmentRequest CreateRequest(Podcast podcast) =>
        new(podcast, [], _fixture.CreateEpisode());

    private YouTubeItemResolver CreateSut(
        YouTubePlaylistFetchFailure? playlistFailure = null,
        YouTubePlaylistFetchFailure? channelVideosFailure = null)
    {
        _mocker.Use<ILogger<YouTubeItemResolver>>(_logger);
        _mocker.GetMock<ICachedTolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(null, Failure: playlistFailure));
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<Podcast>()))
            .Returns(_fixture.CreateTitle());
        _mocker.GetMock<IYouTubeChannelVideosService>()
            .Setup(x => x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new GetChannelVideosResponse(null, null, channelVideosFailure));
        return _mocker.CreateInstance<YouTubeItemResolver>();
    }
}