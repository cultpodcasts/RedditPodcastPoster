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
}
