using System.Linq.Expressions;
using Api.Models;
using Api.Services.Podcasts;
using Api.Services.YouTube;
using Azure.Search.Documents;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Podcasts;

/// <summary>
/// Setting youTubeChannelId on a podcast that already has a playlist is checked against that playlist's owner.
/// A refused change saves nothing (#1179).
/// </summary>
public class PodcastEditYouTubeChannelRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PodcastEditYouTubeChannelRules()
    {
        _mocker.Use(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SearchClient)) as SearchClient);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.Check(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);
    }

    [Fact(DisplayName =
        "PATCH podcast youTubeChannelId that does not own the stored playlist is a BadRequest with youTubePlaylistChannelMismatch and nothing is saved.")]
    public async Task channel_not_owning_playlist_is_refused()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var ownerChannelId = _fixture.CreateYouTubeChannelId();
        var requestedChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = playlistId;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                playlistId, string.Empty, requestedChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.ChannelMismatch, playlistId, ownerChannelId));

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubeChannelId = requestedChannelId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Code.Should().Be(YouTubePlaylistValidation.ChannelMismatchCode);
        result.Error.Should().NotBeNullOrWhiteSpace();
        podcast.YouTubeChannelId.Should().BeEmpty();
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IYouTubePlaylistGuard>().Verify(g =>
            g.CheckChannelAgainstPlaylist(playlistId, string.Empty, requestedChannelId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "PATCH podcast youTubeChannelId that owns the stored playlist is accepted and saved, so a playlist-without-channel parent can get matching identity.")]
    public async Task channel_owning_playlist_is_saved()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = playlistId;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                playlistId, string.Empty, channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((YouTubePlaylistValidation?)null);

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubeChannelId = channelId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        podcast.YouTubeChannelId.Should().Be(channelId);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(podcast), Times.Once);
    }

    [Theory(DisplayName =
        "PATCH podcast youTubeChannelId when the stored playlist is missing or the lookup failed is refused with the playlist code and nothing is saved.")]
    [InlineData(YouTubePlaylistValidationStatus.PlaylistNotFound, PodcastUpdateStatus.BadRequest, "youTubePlaylistNotFound")]
    [InlineData(YouTubePlaylistValidationStatus.LookupFailed, PodcastUpdateStatus.ServiceUnavailable, "youTubePlaylistLookupFailed")]
    public async Task channel_change_playlist_check_failures_are_refused(
        YouTubePlaylistValidationStatus status, PodcastUpdateStatus expected, string code)
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var requestedChannelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = string.Empty;
            p.YouTubePlaylistId = playlistId;
        });
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        _mocker.GetMock<IYouTubePlaylistGuard>()
            .Setup(g => g.CheckChannelAgainstPlaylist(
                playlistId, string.Empty, requestedChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubePlaylistValidation(status, playlistId));

        // Act
        var result = await _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { YouTubeChannelId = requestedChannelId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        result.Code.Should().Be(code);
        podcast.YouTubeChannelId.Should().BeEmpty();
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
    }
}
