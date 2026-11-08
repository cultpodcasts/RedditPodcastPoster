using System.Net;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class IndexPodcastHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private PodcastIndexResult _result = new(PodcastIndexStatus.Ok);

    public IndexPodcastHandlerTests()
    {
        _mocker.GetMock<IPodcastIndexService>()
            .Setup(s => s.IndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _ctx = _mocker.GetMock<IHandlerContext>();
    }

    [Fact(DisplayName =
        "A busy on-demand podcast index is a 409 { reason: indexingInProgress } with the same rejection body as " +
        "catalogue, because the curator must not start a second overlapping pass.")]
    public async Task already_running_is_409_indexing_in_progress()
    {
        // Arrange
        _result = new PodcastIndexResult(PodcastIndexStatus.AlreadyRunning);

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.Handle(_ctx.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<CatalogueIndexRejection>(r =>
                r.Reason == CatalogueIndexRejection.IndexingInProgress &&
                r.Message == CatalogueIndexRejection.IndexingInProgressMessage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A timed-out on-demand podcast index is a 504 { reason: TimedOut } with the same rejection body as catalogue, " +
        "not a 500, because running out of time is not a fault.")]
    public async Task timed_out_is_504_with_reason()
    {
        // Arrange
        _result = new PodcastIndexResult(PodcastIndexStatus.TimedOut);

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.Handle(_ctx.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Json(HttpStatusCode.GatewayTimeout,
            It.Is<CatalogueIndexRejection>(r =>
                r.Reason == CatalogueIndexRejection.TimedOut &&
                r.Message == CatalogueIndexRejection.TimedOutMessage),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        CatalogueIndexRejection.TimedOutMessage.Should().NotContain("was saved");
    }
}
