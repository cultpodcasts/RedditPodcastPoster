using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
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
        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedEpisodes);
        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        youTubeEpisodeProvider.Verify(
            x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()),
            Times.Once);
        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
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
        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId(playlistId),
                new YouTubeChannelId(channelId),
                indexingContext,
                false))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(expectedEpisodes));
        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId(playlistId),
                new YouTubeChannelId(channelId),
                indexingContext,
                false),
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
        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId(playlistId),
                new YouTubeChannelId(channelId),
                indexingContext,
                true))
            .ReturnsAsync(new GetPlaylistEpisodesResponse([]));
        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        // Act
        await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId(playlistId),
                new YouTubeChannelId(channelId),
                indexingContext,
                true),
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
        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(
                null,
                Failure: YouTubePlaylistFetchFailure.NotFound));
        var logger = new Mock<ILogger<YouTubeEpisodeRetrievalHandler>>();
        var sut = new YouTubeEpisodeRetrievalHandler(youTubeEpisodeProvider.Object, logger.Object);

        // Act
        await sut.GetEpisodes(podcast, [], indexingContext);

        // Assert
        logger.Verify(
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
