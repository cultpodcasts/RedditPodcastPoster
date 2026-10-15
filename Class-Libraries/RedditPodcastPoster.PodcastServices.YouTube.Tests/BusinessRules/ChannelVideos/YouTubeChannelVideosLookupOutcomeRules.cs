using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using YouTubeChannel = Google.Apis.YouTube.v3.Data.Channel;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// The channel lookup outcome decides the channel-videos failure and what is logged: only a genuinely missing
/// channel is reported as not found, and a quota failure is not logged a second time.
/// </summary>
public class YouTubeChannelVideosLookupOutcomeRules
{
    private const string UnableToFindChannelPhrase = "Unable to find channel with id";
    private const string LookupFailedPhrase = "Failed to look up channel";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeChannelVideosService> _logger = new();

    [Fact(DisplayName = "When the channel lookup finds no channel, the failure is NotFound and 'Unable to find channel' is logged")]
    public async Task NotFound_Lookup_Reports_NotFound_And_Logs()
    {
        // Arrange
        var sut = CreateSut(YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.NotFound));

        // Act
        var response = await sut.GetChannelVideos(CreateChannelId(), new IndexingContext());

        // Assert
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.NotFound);
        _logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Error && e.Message.Contains(UnableToFindChannelPhrase));
    }

    [Fact(DisplayName = "When the channel lookup hits an API error, the failure is ApiError and it is not reported as not found")]
    public async Task ApiError_Lookup_Reports_ApiError_Not_NotFound()
    {
        // Arrange
        var sut = CreateSut(YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.ApiError));

        // Act
        var response = await sut.GetChannelVideos(CreateChannelId(), new IndexingContext());

        // Assert
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.ApiError);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains(LookupFailedPhrase));
        _logger.Entries.Should().NotContain(e => e.Message.Contains(UnableToFindChannelPhrase));
    }

    [Fact(DisplayName = "When the channel lookup exhausts quota, the failure is QuotaExhausted, not NotFound or ApiError, and nothing more is logged")]
    public async Task QuotaExhausted_Lookup_Reports_QuotaExhausted_Without_Second_Error()
    {
        // Arrange
        var sut = CreateSut(YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.QuotaExhausted));

        // Act
        var response = await sut.GetChannelVideos(CreateChannelId(), new IndexingContext());

        // Assert
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.QuotaExhausted);
        _logger.Entries.Should().BeEmpty();
    }

    [Fact(DisplayName = "When the lookup is skipped, the failure is Skipped and nothing above Debug is logged")]
    public async Task Skipped_Lookup_Reports_Skipped_Quietly()
    {
        // Arrange
        var sut = CreateSut(YouTubeChannelLookupResult.Of(YouTubeChannelLookupOutcome.Skipped));

        // Act
        var response = await sut.GetChannelVideos(CreateChannelId(), new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.Skipped);
        _logger.Entries.Should().NotContain(e => e.Level > LogLevel.Debug);
    }

    [Fact(DisplayName = "When the channel was cached earlier in the pass and YouTube is bypassed, the channel is returned and only the playlist fetch is Skipped")]
    public async Task Cached_Channel_With_Skipped_Playlist_Returns_Channel_And_Skipped()
    {
        // Arrange
        var channel = new YouTubeChannel
        {
            Id = _fixture.CreateYouTubeChannelId(),
            ContentDetails = new ChannelContentDetails
            {
                RelatedPlaylists = new ChannelContentDetails.RelatedPlaylistsData { Uploads = _fixture.CreateYouTubePlaylistId() }
            }
        };
        var sut = CreateSut(YouTubeChannelLookupResult.Found(channel));
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(null, Failure: YouTubePlaylistFetchFailure.Skipped));

        // Act
        var response = await sut.GetChannelVideos(CreateChannelId(), new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Channel.Should().BeSameAs(channel);
        response.PlaylistItems.Should().BeNull();
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.Skipped);
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
    }

    private YouTubeChannelVideosService CreateSut(YouTubeChannelLookupResult lookup)
    {
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(x => x.GetChannelLookup(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(lookup);
        _mocker.Use<ILogger<YouTubeChannelVideosService>>(_logger);
        return _mocker.CreateInstance<YouTubeChannelVideosService>();
    }

    private YouTubeChannelId CreateChannelId() => new(_fixture.CreateYouTubeChannelId());
}