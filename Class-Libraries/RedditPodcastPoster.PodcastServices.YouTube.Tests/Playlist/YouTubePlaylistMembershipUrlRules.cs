using System.Net;
using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
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

    [Fact(DisplayName =
        "When a valid playlist id and watch URL have no matching list=, membership comes from Contains (playlistItems), so YouTubeService is used.")]
    public async Task plain_watch_url_falls_through_to_contains()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var videoId = _fixture.CreateYouTubeId();
        var url = _fixture.DefaultYouTubeUrl(videoId);
        const string body =
            "{\"kind\":\"youtube#playlistItemListResponse\",\"items\":[{\"kind\":\"youtube#playlistItem\",\"id\":\"item-1\"}]}";
        var youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(new FakeHttpMessageHandler(HttpStatusCode.OK, body)),
            ApiKey = "synthetic-key"
        });
        var wrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(youTubeService);
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();

        // Act
        var result = await sut.IsVideoInPlaylist(
            wrapper.Object, playlistId, url, skipYouTubeUrlResolving: false);

        // Assert
        result.Should().BeTrue();
        wrapper.VerifyGet(x => x.YouTubeService, Times.AtLeastOnce);
    }

    [Fact(DisplayName =
        "When the URL has no extractable video id, membership is unknown (null) and YouTubeService is never accessed.")]
    public async Task missing_video_id_is_unknown_without_api()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var url = new Uri("https://www.youtube.com/watch");
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();

        // Act
        var result = await sut.IsVideoInPlaylist(
            _mocker.GetMock<IYouTubeServiceWrapper>().Object, playlistId, url, skipYouTubeUrlResolving: false);

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<IYouTubeServiceWrapper>().VerifyGet(x => x.YouTubeService, Times.Never);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args) =>
            new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
    }
}
