using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Handlers;

/// <summary>
/// The retrieval handler reports a playlist fetch failure only when one really happened: a skipped or
/// quota-exhausted fetch logs nothing, a NotFound or ApiError still logs an error.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerFetchFailureRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeRetrievalHandler> _logger = new();

    [Theory(DisplayName = "When the playlist fetch is skipped or quota-exhausted, the handler logs no fetch failure")]
    [InlineData(YouTubePlaylistFetchFailure.Skipped)]
    [InlineData(YouTubePlaylistFetchFailure.QuotaExhausted)]
    public async Task Skipped_Or_Quota_Failure_Logs_No_Error(YouTubePlaylistFetchFailure failure)
    {
        // Arrange
        var podcast = CreatePlaylistPodcast();
        var sut = CreateSut(failure);

        // Act
        await sut.GetEpisodes(podcast, [], new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("was not found"));
    }

    [Theory(DisplayName = "When the playlist fetch really failed, the handler still logs an error")]
    [InlineData(YouTubePlaylistFetchFailure.NotFound, "was not found")]
    [InlineData(YouTubePlaylistFetchFailure.ApiError, "playlist fetch failed")]
    public async Task Real_Failure_Still_Logs_Error(YouTubePlaylistFetchFailure failure, string expectedPhrase)
    {
        // Arrange
        var podcast = CreatePlaylistPodcast();
        var sut = CreateSut(failure);

        // Act
        await sut.GetEpisodes(podcast, [], new IndexingContext());

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(expectedPhrase));
    }

    private Podcast CreatePlaylistPodcast() =>
        _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });

    private YouTubeEpisodeRetrievalHandler CreateSut(YouTubePlaylistFetchFailure failure)
    {
        _mocker.Use<ILogger<YouTubeEpisodeRetrievalHandler>>(_logger);
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>(),
                It.IsAny<IEnumerable<string>?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(null, Failure: failure));
        return _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();
    }
}
