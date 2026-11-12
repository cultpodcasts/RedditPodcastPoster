using Api.Dtos;
using Api.Handlers;
using Api.Handlers.News;
using Api.Models;
using Api.Services.News;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>#1397: status codes and bodies of the report move between news organisations and the empty-organisation delete.</summary>
public class NewsOrganisationReportsMoveHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public NewsOrganisationReportsMoveHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.NotFound(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.BadRequest(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
    }

    private static object? Property(object body, string name) => body.GetType().GetProperty(name)?.GetValue(body);

    private NewsReportOrganisationMoveCommand Command() =>
        new(_fixture.CreateGuid(), _fixture.CreateGuid(), null, false);

    private void MoveReturns(NewsReportOrganisationMoveResult result) =>
        _mocker.GetMock<INewsReportOrganisationMoveService>()
            .Setup(s => s.MoveAsync(It.IsAny<NewsReportOrganisationMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact(DisplayName =
        "An accepted report move responds 202 with the moved ids, the remaining count and the emptied source, so the caller can re-send until empty and then retire the source.")]
    public async Task accepted_move_returns_202_with_the_outcome()
    {
        // Arrange
        var sourceId = _fixture.CreateGuid();
        var reportId = _fixture.CreateGuid();
        MoveReturns(new NewsReportOrganisationMoveResult(
            NewsReportOrganisationMoveStatus.Accepted, sourceId, _fixture.CreateGuid(), _fixture.CreateTitle(),
            [reportId]) { RemainingReportCount = 0, EmptiedNewsOrganisationId = sourceId });
        var sut = _mocker.CreateInstance<PostNewsReportOrganisationMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, Command(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<NewsReportOrganisationMoveResponse>(r =>
                r.MovedReportIds.Single() == reportId &&
                r.RemainingReportCount == 0 &&
                r.EmptiedNewsOrganisationId == sourceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A refused move responds 409 or 404 with the per-id lists, an invalid one 400, and a failed one 500 with failureIndexing.")]
    public async Task refused_and_failed_moves_map_to_their_status_codes()
    {
        // Arrange
        var conflictId = _fixture.CreateGuid();
        var sut = _mocker.CreateInstance<PostNewsReportOrganisationMoveHandler>();

        // Act
        MoveReturns(new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.Conflict,
            ConflictReportIds: [conflictId]));
        await sut.Handle(_ctx.Object, Command(), CancellationToken.None);
        MoveReturns(new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.NotFound));
        await sut.Handle(_ctx.Object, Command(), CancellationToken.None);
        MoveReturns(new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.BadRequest,
            Error: _fixture.Create<string>()));
        await sut.Handle(_ctx.Object, Command(), CancellationToken.None);
        MoveReturns(new NewsReportOrganisationMoveResult(NewsReportOrganisationMoveStatus.Failed,
            FailureIndexing: true));
        await sut.Handle(_ctx.Object, Command(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<NewsReportOrganisationMoveResponse>(r => r.ConflictReportIds.Single() == conflictId),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.NotFound(It.IsAny<NewsReportOrganisationMoveResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _ctx.Verify(x => x.BadRequest(It.IsAny<ApiErrorResponse>(), It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(
            It.Is<NewsReportOrganisationMoveResponse>(r => r.FailureIndexing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Deleting an organisation that still has reports responds 409 with the remaining count, and an empty one 200.")]
    public async Task delete_maps_has_reports_to_409_and_deleted_to_200()
    {
        // Arrange
        var remaining = _fixture.Create<int>() % 50 + 1;
        var service = _mocker.GetMock<INewsOrganisationDeleteService>();
        var sut = _mocker.CreateInstance<DeleteNewsOrganisationHandler>();

        // Act
        service.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.HasReports, remaining));
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);
        service.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Deleted));
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<object>(body => Equals(Property(body, "remainingReportCount"), remaining)),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.Ok(), Times.Once);
    }
}
