using System.Net;
using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// A deleted or rejected playlist reads as "the video is not in it", so one dead playlist cannot block routing for
/// a whole shared channel. Only a check that could not be made (quota, outage) is unknown (null).
/// </summary>
public class YouTubePlaylistMembershipErrorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private async Task<bool?> ContainsWhenYouTubeAnswers(HttpStatusCode status, string body)
    {
        var youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(new FakeHttpMessageHandler(status, body)),
            ApiKey = "synthetic-key"
        });
        var wrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(youTubeService);
        var sut = _mocker.CreateInstance<YouTubePlaylistMembership>();
        return await sut.Contains(wrapper.Object, _fixture.CreateYouTubePlaylistId(), _fixture.CreateYouTubeId());
    }

    [Fact(DisplayName = "A 404 playlistNotFound from YouTube means the video is not in the playlist (false), not unknown.")]
    public async Task not_found_playlist_is_false()
    {
        // Arrange
        const string body =
            "{\"error\":{\"code\":404,\"message\":\"The playlist identified with the request's playlistId parameter cannot be found.\",\"errors\":[{\"reason\":\"playlistNotFound\"}]}}";

        // Act
        var result = await ContainsWhenYouTubeAnswers(HttpStatusCode.NotFound, body);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "A 400 for a playlist id YouTube rejects means the video is not in the playlist (false), not unknown.")]
    public async Task bad_request_playlist_is_false()
    {
        // Arrange
        const string body = "{\"error\":{\"code\":400,\"message\":\"Invalid value\",\"errors\":[{\"reason\":\"invalidParameter\"}]}}";

        // Act
        var result = await ContainsWhenYouTubeAnswers(HttpStatusCode.BadRequest, body);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Quota exhaustion is still unknown (null), so routing fails closed on a transient failure.")]
    public async Task quota_is_unknown()
    {
        // Arrange
        const string body =
            "{\"error\":{\"code\":403,\"message\":\"The request cannot be completed because you have exceeded your quota.\",\"errors\":[{\"reason\":\"quotaExceeded\"}]}}";

        // Act
        var result = await ContainsWhenYouTubeAnswers(HttpStatusCode.Forbidden, body);

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>().Verify(x => x.RecordQuotaHitAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(),
            YouTubeQuotaOperation.PlaylistItemsList, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "A YouTube outage (5xx) is still unknown (null), so routing fails closed.")]
    public async Task outage_is_unknown()
    {
        // Arrange
        const string body = "{\"error\":{\"code\":500,\"message\":\"Backend Error\"}}";

        // Act
        var result = await ContainsWhenYouTubeAnswers(HttpStatusCode.InternalServerError, body);

        // Assert
        result.Should().BeNull();
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
