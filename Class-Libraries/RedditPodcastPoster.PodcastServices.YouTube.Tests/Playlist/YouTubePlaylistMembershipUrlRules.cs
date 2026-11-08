using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// Shared URL-shaped playlist membership used by PodcastService and CatalogueParentLookup (#1332).
/// </summary>
public class YouTubePlaylistMembershipUrlRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubePlaylistMembershipUrlRules()
    {
        _mocker.GetMock<IYouTubeServiceWrapper>();
    }

    [Fact(DisplayName =
        "A blank playlist id means channel-wide ownership, so membership is true without calling YouTube.")]
    public async Task blank_playlist_id_is_channel_wide_true()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();
        var url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId());

        // Act
        var result = await sut.IsVideoInPlaylist(
            _mocker.GetMock<IYouTubeServiceWrapper>().Object, null, url, skipYouTubeUrlResolving: false);

        // Assert
        result.Should().BeTrue();
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
    }

    [Fact(DisplayName =
        "When the URL list= already names the stored playlist, membership is true without a playlistItems call.")]
    public async Task url_list_param_matching_playlist_is_true_without_api()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var videoId = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.youtube.com/watch?v={videoId}&list={playlistId}");
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();

        // Act
        var result = await sut.IsVideoInPlaylist(
            _mocker.GetMock<IYouTubeServiceWrapper>().Object, playlistId, url, skipYouTubeUrlResolving: false);

        // Assert
        result.Should().BeTrue();
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
    }

    [Fact(DisplayName =
        "A malformed stored playlist id is treated as not containing the video (false), with no YouTube call.")]
    public async Task malformed_playlist_id_is_false_without_api()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();
        var url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId());

        // Act
        var result = await sut.IsVideoInPlaylist(
            _mocker.GetMock<IYouTubeServiceWrapper>().Object, "PL@not-valid", url, skipYouTubeUrlResolving: false);

        // Assert
        result.Should().BeFalse();
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
    }

    [Fact(DisplayName =
        "When SkipYouTubeUrlResolving is set, membership is unknown (null) so callers fail closed.")]
    public async Task skip_resolving_is_unknown()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();
        var url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId());

        // Act
        var result = await sut.IsVideoInPlaylist(
            _mocker.GetMock<IYouTubeServiceWrapper>().Object,
            _fixture.CreateYouTubePlaylistId(),
            url,
            skipYouTubeUrlResolving: true);

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
    }
}
