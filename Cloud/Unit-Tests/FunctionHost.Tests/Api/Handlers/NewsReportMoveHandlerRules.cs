using Api.Dtos;
using Api.Handlers;
using Api.Handlers.News;
using Api.Models;
using Api.Services.News;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using EpisodeFilmMoveRequest = Api.Models.EpisodeFilmMoveRequest;
using EpisodeTvShowMoveRequest = Api.Models.EpisodeTvShowMoveRequest;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>#1368: the news report move handlers answer with the same status codes and bodies as the episode moves.</summary>
public class NewsReportMoveHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public NewsReportMoveHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Ok(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
    }

    [Fact(DisplayName =
        "PostNewsReportTvShowMoveHandler: an accepted move responds 202 with the TV-move DTO carrying emptiedNewsOrganisationId, so the website can offer to retire the emptied organisation.")]
    public async Task tv_show_move_accepted_returns_dto_with_emptied_organisation()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var reportId = _fixture.CreateGuid();
        var organisationId = _fixture.CreateGuid();
        _mocker.GetMock<INewsReportTvShowMoveService>()
            .Setup(s => s.MoveAsync(It.IsAny<NewsReportTvShowMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeTvShowMoveResult(
                EpisodeTvShowMoveStatus.Accepted, showId, _fixture.CreateTitle(), reportId,
                EmptiedNewsOrganisationId: organisationId));
        var sut = _mocker.CreateInstance<PostNewsReportTvShowMoveHandler>();

        // Act
        await sut.Handle(
            _ctx.Object,
            new NewsReportTvShowMoveCommand(reportId, new EpisodeTvShowMoveRequest { TvShowId = showId }),
            CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<EpisodeTvShowMoveResponse>(r =>
                r.TvShowId == showId &&
                r.EpisodeId == reportId &&
                r.EmptiedNewsOrganisationId == organisationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostNewsReportTvShowMoveHandler: a failed move responds 500 with the TV-move DTO including failureIndexing, the same contract as the episode moves.")]
    public async Task tv_show_move_failed_returns_dto_with_failure_indexing()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var reportId = _fixture.CreateGuid();
        _mocker.GetMock<INewsReportTvShowMoveService>()
            .Setup(s => s.MoveAsync(It.IsAny<NewsReportTvShowMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeTvShowMoveResult(
                EpisodeTvShowMoveStatus.Failed, showId, _fixture.CreateTitle(), reportId, FailureIndexing: true));
        var sut = _mocker.CreateInstance<PostNewsReportTvShowMoveHandler>();

        // Act
        await sut.Handle(
            _ctx.Object,
            new NewsReportTvShowMoveCommand(reportId, new EpisodeTvShowMoveRequest { TvShowId = showId }),
            CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeTvShowMoveResponse>(r => r.FailureIndexing && r.TvShowId == showId && r.EpisodeId == reportId),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(It.Is<ApiErrorResponse>(_ => true), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "PostNewsReportFilmMoveHandler: a failed move responds 500 with the film-move DTO including failureIndexing, and an ambiguous identity responds 200 with the candidates, as Move to film does.")]
    public async Task film_move_failed_and_identity_hold_follow_film_move_contract()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var service = _mocker.GetMock<INewsReportFilmMoveService>();
        service
            .SetupSequence(s => s.MoveAsync(It.IsAny<NewsReportFilmMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.Failed, filmId, _fixture.CreateTitle(), FailureIndexing: true))
            .ReturnsAsync(new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Cancelled));
        var sut = _mocker.CreateInstance<PostNewsReportFilmMoveHandler>();
        var command = new NewsReportFilmMoveCommand(filmId, new EpisodeFilmMoveRequest());

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeFilmMoveResponse>(r => r.FailureIndexing && r.FilmId == filmId),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.Ok(
            It.Is<EpisodeFilmMoveResponse>(r => r.Cancelled),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
