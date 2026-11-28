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
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class IndexPodcastHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private PodcastIndexResult _result = new(PodcastIndexStatus.Ok);
    private PodcastIndexPreviewResult _previewResult = new(PodcastIndexStatus.Ok);

    public IndexPodcastHandlerTests()
    {
        _mocker.GetMock<IPodcastIndexService>()
            .Setup(s => s.IndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _mocker.GetMock<IPodcastIndexService>()
            .Setup(s => s.IndexBacklogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _mocker.GetMock<IPodcastIndexService>()
            .Setup(s => s.PreviewBacklogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _previewResult);
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

    [Fact(DisplayName =
        "When the controller routes backlog index, the handler calls IndexBacklogAsync so Azure walks the full catalogue.")]
    public async Task backlog_route_calls_index_backlog_async()
    {
        // Arrange
        _result = new PodcastIndexResult(PodcastIndexStatus.AlreadyRunning);
        var podcastName = _fixture.CreateTitle();

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.HandleBacklog(_ctx.Object, podcastName, CancellationToken.None);

        // Assert
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.IndexBacklogAsync(podcastName, It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.IndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _ctx.Verify(c => c.Query(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the controller routes default hub Index, the handler calls IndexAsync so ReleasedDaysAgo lookback stays.")]
    public async Task default_route_calls_index_async()
    {
        // Arrange
        _result = new PodcastIndexResult(PodcastIndexStatus.AlreadyRunning);
        var podcastName = _fixture.CreateTitle();

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.Handle(_ctx.Object, podcastName, CancellationToken.None);

        // Assert
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.IndexAsync(podcastName, It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.IndexBacklogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _ctx.Verify(c => c.Query(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "A backlog preview answers 200 with the new and ignored episode counts and runs no index, because the confirmation is a query that changes nothing.")]
    public async Task backlog_preview_ok_returns_counts_and_never_indexes()
    {
        // Arrange
        var preview = new IndexPreviewResponse(IndexStatus.Performed, _fixture.Create<int>() % 500 + 1, 3);
        _previewResult = new PodcastIndexPreviewResult(PodcastIndexStatus.Ok, preview);
        var podcastName = _fixture.CreateTitle();

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.HandleBacklogPreview(_ctx.Object, podcastName, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Ok(
            It.Is<IndexPodcastBacklogPreviewResponse>(r =>
                r.NewEpisodeCount == preview.NewEpisodeCount &&
                r.IgnoredEpisodeCount == preview.IgnoredEpisodeCount),
            It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.PreviewBacklogAsync(podcastName, It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<IPodcastIndexService>().Verify(
            s => s.IndexBacklogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A backlog preview of an unknown podcast is a 404 carrying the index status, because the curator must see the podcast was not found.")]
    public async Task backlog_preview_not_found_is_404()
    {
        // Arrange
        _previewResult = new PodcastIndexPreviewResult(
            PodcastIndexStatus.NotFound,
            new IndexPreviewResponse(IndexStatus.NotFound));

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.HandleBacklogPreview(_ctx.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.NotFound(
            It.Is<IndexPodcastResponse>(r => r.IndexStatus == IndexStatus.NotFound),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A backlog preview of a podcast the index would not run on is a 400 carrying the index status, because there is nothing to confirm.")]
    public async Task backlog_preview_not_performed_is_400()
    {
        // Arrange
        _previewResult = new PodcastIndexPreviewResult(
            PodcastIndexStatus.BadRequest,
            new IndexPreviewResponse(IndexStatus.NotPerformed));

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.HandleBacklogPreview(_ctx.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.BadRequest(
            It.Is<IndexPodcastResponse>(r => r.IndexStatus == IndexStatus.NotPerformed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A timed-out backlog preview is a 504 { reason: TimedOut } with the same rejection body as the index, not a 500, because running out of time is not a fault.")]
    public async Task backlog_preview_timed_out_is_504_with_reason()
    {
        // Arrange
        _previewResult = new PodcastIndexPreviewResult(
            PodcastIndexStatus.TimedOut,
            new IndexPreviewResponse(IndexStatus.TimedOut));

        // Act
        var sut = _mocker.CreateInstance<IndexPodcastHandler>();
        await sut.HandleBacklogPreview(_ctx.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Json(HttpStatusCode.GatewayTimeout,
            It.Is<CatalogueIndexRejection>(r =>
                r.Reason == CatalogueIndexRejection.TimedOut &&
                r.Message == CatalogueIndexRejection.TimedOutMessage),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }}
