using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// The episode provider must not raise a playlist fetch error for a fetch that was skipped or whose quota failure
/// was already logged, but must still raise it for a genuine NotFound or ApiError.
/// </summary>
public class YouTubeEpisodeProviderSkippedFetchRules
{
    private const string NotFoundPhrase = "was not found";
    private const string FetchFailedPhrase = "playlist fetch failed";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeProvider> _logger = new();

    [Theory(DisplayName = "When a channel-only fetch is skipped or quota-exhausted, the provider logs no fetch failure")]
    [InlineData(YouTubePlaylistFetchFailure.Skipped)]
    [InlineData(YouTubePlaylistFetchFailure.QuotaExhausted)]
    public async Task Skipped_Or_Quota_Fetch_Logs_No_Error(YouTubePlaylistFetchFailure failure)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var sut = CreateSut(failure);

        // Act
        var episodes = await sut.GetEpisodes(podcast, new IndexingContext(SkipYouTubeUrlResolving: true), knownIds: []);

        // Assert
        episodes.Should().BeNull();
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _logger.Entries.Should().NotContain(e => e.Message.Contains(NotFoundPhrase));
    }

    [Fact(DisplayName = "When a channel-only fetch is genuinely not found, the provider still logs playlist-not-found")]
    public async Task NotFound_Fetch_Still_Logs()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var sut = CreateSut(YouTubePlaylistFetchFailure.NotFound);

        // Act
        await sut.GetEpisodes(podcast, new IndexingContext(), knownIds: []);

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(NotFoundPhrase));
    }

    [Fact(DisplayName = "When a channel-only fetch hits an API error, the provider logs a fetch failure and not playlist-not-found")]
    public async Task ApiError_Fetch_Logs_Fetch_Failure()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var sut = CreateSut(YouTubePlaylistFetchFailure.ApiError);

        // Act
        await sut.GetEpisodes(podcast, new IndexingContext(), knownIds: []);

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(FetchFailedPhrase));
        _logger.Entries.Should().NotContain(e => e.Message.Contains(NotFoundPhrase));
    }

    private YouTubeEpisodeProvider CreateSut(YouTubePlaylistFetchFailure failure)
    {
        _mocker.Use<ILogger<YouTubeEpisodeProvider>>(_logger);
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<RedditPodcastPoster.Models.Podcasts.Podcast>()))
            .Returns(_fixture.CreateTitle());
        _mocker.GetMock<IYouTubeChannelVideosService>()
            .Setup(x => x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new GetChannelVideosResponse(null, null, failure));
        return _mocker.CreateInstance<YouTubeEpisodeProvider>();
    }
}
