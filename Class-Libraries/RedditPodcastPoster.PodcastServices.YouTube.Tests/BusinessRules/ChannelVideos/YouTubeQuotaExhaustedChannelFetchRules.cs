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
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// When every api-key is out of quota on a pass where YouTube is enabled, the channel-only fetch records quota
/// exhaustion once. It is not relabelled as a missing channel or as a playlist API failure.
/// </summary>
public class YouTubeQuotaExhaustedChannelFetchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeProvider> _providerLogger = new();

    [Fact(DisplayName = "When the api-key ring is exhausted, a channel-only fetch is neither not-found nor an API error")]
    public async Task Exhausted_Ring_Is_Not_Reported_As_NotFound_Or_ApiError()
    {
        // Arrange
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.CanRotate).Returns(true);
        _mocker.GetMock<IYouTubeServiceWrapper>().Setup(x => x.Rotate()).Throws(new InvalidOperationException());
        _mocker.GetMock<IYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new YouTubeQuotaException());
        _mocker.Use<ITolerantYouTubeChannelService>(_mocker.CreateInstance<TolerantYouTubeChannelService>());
        var channelVideosService = _mocker.CreateInstance<YouTubeChannelVideosService>();
        _mocker.Use<IYouTubeChannelVideosService>(channelVideosService);
        _mocker.Use<ILogger<YouTubeEpisodeProvider>>(_providerLogger);
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<RedditPodcastPoster.Models.Podcasts.Podcast>()))
            .Returns(_fixture.CreateTitle());
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = channelId);
        var indexingContext = new IndexingContext();
        var provider = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var channelVideos = await channelVideosService.GetChannelVideos(new YouTubeChannelId(channelId), indexingContext);
        var episodes = await provider.GetEpisodes(podcast, indexingContext, knownIds: []);

        // Assert
        channelVideos.Failure.Should().Be(YouTubePlaylistFetchFailure.QuotaExhausted);
        channelVideos.Failure.Should().NotBe(YouTubePlaylistFetchFailure.NotFound);
        channelVideos.Failure.Should().NotBe(YouTubePlaylistFetchFailure.ApiError);
        episodes.Should().BeNull();
        _providerLogger.Entries.Should().NotContain(e => e.Message.Contains("was not found"));
        _providerLogger.Entries.Should().NotContain(e => e.Message.Contains("ApiError"));
        _providerLogger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        indexingContext.YouTubeQuotaExhausted.Should().BeTrue();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }
}
