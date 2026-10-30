using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Api.Models;
using Api.Services.Podcasts;
using AutoFixture;
using Xunit;
using PodcastChangeRequest = Api.Models.PodcastChangeRequest;

namespace FunctionHost.Tests.Api.Handlers;

public class PostPodcastHandlerTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastUpdateResult _result = new(PodcastUpdateStatus.Accepted);

    public PostPodcastHandlerTests()
    {
        _mocker.GetMock<IPodcastUpdateService>()
            .Setup(s => s.UpdateAsync(It.IsAny<PodcastChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
    }

    [Fact(DisplayName =
        "POST podcast: when the update is rejected as a bad request (e.g. an invalid description-regex), the " +
        "handler returns 400 with the service's error message in the ApiErrorResponse body, so the website can " +
        "show it.")]
    public async Task bad_request_maps_to_400_with_error()
    {
        // Arrange
        var podcastId = _fixture.Create<Guid>();
        var error = _fixture.Create<string>();
        _result = new PodcastUpdateResult(PodcastUpdateStatus.BadRequest, podcastId, Error: error);
        var ctx = _mocker.GetMock<IHandlerContext>();
        var sut = _mocker.CreateInstance<PostPodcastHandler>();

        // Act
        await sut.Handle(ctx.Object, new PodcastChangeRequestWrapper(podcastId, new PodcastChangeRequest()),
            CancellationToken.None);

        // Assert
        ctx.Verify(x => x.BadRequest(It.Is<ApiErrorResponse>(r => r.Error == error), It.IsAny<CancellationToken>()),
            Times.Once);
        ctx.Verify(x => x.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "POST podcast: a playlist-ownership refusal returns 400 with the stable code in the body, so the website can explain it.")]
    public async Task ownership_bad_request_includes_code()
    {
        // Arrange
        var podcastId = _fixture.Create<Guid>();
        _result = new PodcastUpdateResult(PodcastUpdateStatus.BadRequest, podcastId, Error: "mismatch",
            Code: "youTubePlaylistChannelMismatch");
        var ctx = _mocker.GetMock<IHandlerContext>();
        var sut = _mocker.CreateInstance<PostPodcastHandler>();

        // Act
        await sut.Handle(ctx.Object, new PodcastChangeRequestWrapper(podcastId, new PodcastChangeRequest()),
            CancellationToken.None);

        // Assert
        ctx.Verify(x => x.BadRequest(It.Is<ApiErrorResponse>(r =>
            r.Error == "mismatch" && r.Code == "youTubePlaylistChannelMismatch" && r.Retryable == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "POST podcast: when the playlist owner could not be looked up, the handler returns a retryable 503 with the code; nothing was saved.")]
    public async Task service_unavailable_maps_to_503_retryable()
    {
        // Arrange
        var podcastId = _fixture.Create<Guid>();
        _result = new PodcastUpdateResult(PodcastUpdateStatus.ServiceUnavailable, podcastId, Error: "try again",
            Code: "youTubePlaylistOwnerLookupFailed");
        var ctx = _mocker.GetMock<IHandlerContext>();
        var sut = _mocker.CreateInstance<PostPodcastHandler>();

        // Act
        await sut.Handle(ctx.Object, new PodcastChangeRequestWrapper(podcastId, new PodcastChangeRequest()),
            CancellationToken.None);

        // Assert
        ctx.Verify(x => x.Json(System.Net.HttpStatusCode.ServiceUnavailable, It.Is<ApiErrorResponse>(r =>
            r.Code == "youTubePlaylistOwnerLookupFailed" && r.Retryable == true), It.IsAny<CancellationToken>()),
            Times.Once);
        ctx.Verify(x => x.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
