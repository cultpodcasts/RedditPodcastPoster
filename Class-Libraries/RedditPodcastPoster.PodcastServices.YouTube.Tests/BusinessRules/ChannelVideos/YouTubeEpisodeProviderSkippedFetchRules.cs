using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// The episode provider must not raise "playlist was not found" for a Skipped fetch, but must still raise it
/// for a genuine NotFound (real case: The Cult on the Cape, channel UC1hYXZi0_9KdI4z0qtSCIkQ, empty playlist id).
/// </summary>
public class YouTubeEpisodeProviderSkippedFetchRules
{
    private const string CultOnTheCapeChannelId = "UC1hYXZi0_9KdI4z0qtSCIkQ";
    private const string NotFoundPhrase = "was not found";

    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeProvider> _logger = new();

    private async Task<IList<RedditPodcastPoster.Models.Episodes.Episode>?> Run(YouTubePlaylistFetchFailure failure)
    {
        _mocker.Use<ILogger<YouTubeEpisodeProvider>>(_logger);
        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<Podcast>()))
            .Returns("channel-only podcast uses the uploads playlist");
        _mocker.GetMock<IYouTubeChannelVideosService>()
            .Setup(x => x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new GetChannelVideosResponse(null, null, failure));
        var podcast = new Podcast
        {
            Name = "The Cult on the Cape", YouTubeChannelId = CultOnTheCapeChannelId, YouTubePlaylistId = string.Empty
        };
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();
        return await sut.GetEpisodes(podcast, new IndexingContext(SkipYouTubeUrlResolving: true), knownIds: []);
    }

    [Fact(DisplayName = "Skipped channel-videos fetch logs no error and no playlist-not-found")]
    public async Task Skipped_Fetch_Logs_No_Error()
    {
        // Arrange
        const YouTubePlaylistFetchFailure failure = YouTubePlaylistFetchFailure.Skipped;

        // Act
        var episodes = await Run(failure);

        // Assert
        episodes.Should().BeNull();
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _logger.Entries.Should().NotContain(e => e.Message.Contains(NotFoundPhrase));
    }

    [Fact(DisplayName = "Genuine NotFound still logs playlist-not-found")]
    public async Task NotFound_Fetch_Still_Logs()
    {
        // Arrange
        const YouTubePlaylistFetchFailure failure = YouTubePlaylistFetchFailure.NotFound;

        // Act
        await Run(failure);

        // Assert
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(NotFoundPhrase));
    }
}
