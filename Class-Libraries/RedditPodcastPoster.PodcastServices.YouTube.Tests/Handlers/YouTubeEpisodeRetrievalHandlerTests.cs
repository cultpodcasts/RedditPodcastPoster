using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Handlers;

/// <summary>
/// Handler wiring: SkipExpensiveYouTubeQueries must choose channel vs single-page vs paginated playlist paths.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IYouTubeEpisodeRetrievalHandler Sut => _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

    [Fact(DisplayName =
        "When the podcast has a channel id but no playlist and SkipExpensiveYouTubeQueries is set, GetEpisodes still calls channel discovery " +
        "because channel Search.List is not the expensive playlist walk.")]
    public async Task Channel_only_still_calls_channel_discovery_when_expensive_queries_skipped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = null!;
        });
        var indexingContext = new IndexingContext(
            DomainTestFixture.UtcDaysAgo(2),
            SkipExpensiveYouTubeQueries: true);
        var expectedEpisodes = new List<EpisodeModel>
        {
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast)
        };
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedEpisodes);

        // Act
        var result = await Sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        _mocker.GetMock<IYouTubeEpisodeProvider>().Verify(
            x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()),
            Times.Once);
        _mocker.GetMock<IYouTubeEpisodeProvider>().Verify(
            x => x.GetPlaylistEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the playlist is marked expensive and SkipExpensiveYouTubeQueries is set, GetEpisodes uses a single-page playlist fetch " +
        "because full playlist pagination must not run on that pass.")]
    public async Task Expensive_playlist_uses_single_page_when_expensive_queries_skipped()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
            p.YouTubePlaylistQueryIsExpensive = true;
        });
        var indexingContext = new IndexingContext(
            DomainTestFixture.UtcDaysAgo(2),
            SkipExpensiveYouTubeQueries: true);
        var expectedEpisodes = new List<EpisodeModel>
        {
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast)
        };
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(
                podcast,
                It.Is<YouTubePlaylistId>(y => y.PlaylistId == playlistId),
                It.Is<YouTubeChannelId>(y => y.ChannelId == channelId),
                indexingContext,
                false,
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(expectedEpisodes));

        // Act
        var result = await Sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        _mocker.GetMock<IYouTubeEpisodeProvider>().Verify(
            x => x.GetPlaylistEpisodes(
                podcast,
                It.Is<YouTubePlaylistId>(y => y.PlaylistId == playlistId),
                It.Is<YouTubeChannelId>(y => y.ChannelId == channelId),
                indexingContext,
                false,
                It.IsAny<PlaylistOrder?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When the playlist is marked expensive and expensive queries are allowed, GetEpisodes requests expensive playlist pagination " +
        "because that pass may walk the ascending playlist.")]
    public async Task Expensive_playlist_uses_pagination_when_expensive_queries_allowed()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
            p.YouTubePlaylistQueryIsExpensive = true;
        });
        var indexingContext = new IndexingContext(
            DomainTestFixture.UtcDaysAgo(2),
            SkipExpensiveYouTubeQueries: false);
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(
                podcast,
                It.Is<YouTubePlaylistId>(y => y.PlaylistId == playlistId),
                It.Is<YouTubeChannelId>(y => y.ChannelId == channelId),
                indexingContext,
                true,
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse([]));

        // Act
        await Sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        _mocker.GetMock<IYouTubeEpisodeProvider>().Verify(
            x => x.GetPlaylistEpisodes(
                podcast,
                It.Is<YouTubePlaylistId>(y => y.PlaylistId == playlistId),
                It.Is<YouTubeChannelId>(y => y.ChannelId == channelId),
                indexingContext,
                true,
                It.IsAny<PlaylistOrder?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When playlist discovery reports NotFound, GetEpisodes logs the podcast name and playlist id " +
        "because a deleted or private playlist needs a new YouTubePlaylistId on that podcast.")]
    public async Task Playlist_not_found_logs_podcast_name_and_playlist_id()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = playlistId;
        });
        var indexingContext = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(
                podcast,
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(
                null,
                Failure: YouTubePlaylistFetchFailure.NotFound));

        // Act
        await Sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        _mocker.GetMock<ILogger<YouTubeEpisodeRetrievalHandler>>().Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(podcast.Name) &&
                    state.ToString()!.Contains(playlistId) &&
                    state.ToString()!.Contains("not found", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
