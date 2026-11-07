using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// #1098: Failed trailer-attach / film-move / TV-move must return the command DTO on 500
/// so <c>failureIndexing</c> reaches the client (docs/film-trailers.md).
/// </summary>
public class EpisodeMoveFailedHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public EpisodeMoveFailedHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
    }

    [Fact(DisplayName =
        "PostEpisodeTrailerAttachHandler: when AttachAsync returns Failed with failureIndexing, then Handle responds 500 with the attach DTO including failureIndexing, because docs/film-trailers.md promises that flag on indexing failure.")]
    public async Task trailer_attach_failed_returns_dto_with_failure_indexing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var filmName = _fixture.CreateTitle();
        _mocker.GetMock<IEpisodeTrailerAttachService>()
            .Setup(s => s.AttachAsync(It.IsAny<EpisodeTrailerAttachCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeTrailerAttachResult(
                EpisodeTrailerAttachStatus.Failed, filmId, filmName, FailureIndexing: true));
        var command = new EpisodeTrailerAttachCommand(
            _fixture.CreateGuid(),
            _fixture.CreateGuid(),
            new EpisodeTrailerAttachRequest { FilmId = filmId });
        var sut = _mocker.CreateInstance<PostEpisodeTrailerAttachHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeTrailerAttachResponse>(r =>
                r.FailureIndexing &&
                r.FilmId == filmId &&
                r.Name == filmName),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(
            It.Is<ApiErrorResponse>(_ => true),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "PostEpisodeFilmMoveHandler: when MoveAsync returns Failed with failureIndexing, then Handle responds 500 with the film-move DTO including failureIndexing, because the website and docs read that flag on indexing failure.")]
    public async Task film_move_failed_returns_dto_with_failure_indexing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var filmName = _fixture.CreateTitle();
        _mocker.GetMock<IEpisodeFilmMoveService>()
            .Setup(s => s.MoveAsync(It.IsAny<EpisodeFilmMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.Failed, filmId, filmName, FailureIndexing: true));
        var command = new EpisodeFilmMoveCommand(
            _fixture.CreateGuid(),
            _fixture.CreateGuid(),
            new EpisodeFilmMoveRequest());
        var sut = _mocker.CreateInstance<PostEpisodeFilmMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeFilmMoveResponse>(r =>
                r.FailureIndexing &&
                r.FilmId == filmId &&
                r.Name == filmName),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(
            It.Is<ApiErrorResponse>(_ => true),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "PostEpisodeTvShowMoveHandler: when MoveAsync returns Failed with failureIndexing, then Handle responds 500 with the TV-move DTO including failureIndexing, because the same contract as film move applies.")]
    public async Task tv_show_move_failed_returns_dto_with_failure_indexing()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var showName = _fixture.CreateTitle();
        var episodeId = _fixture.CreateGuid();
        _mocker.GetMock<IEpisodeTvShowMoveService>()
            .Setup(s => s.MoveAsync(It.IsAny<EpisodeTvShowMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeTvShowMoveResult(
                EpisodeTvShowMoveStatus.Failed, showId, showName, episodeId, FailureIndexing: true));
        var command = new EpisodeTvShowMoveCommand(
            _fixture.CreateGuid(),
            episodeId,
            new EpisodeTvShowMoveRequest { TvShowId = showId });
        var sut = _mocker.CreateInstance<PostEpisodeTvShowMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeTvShowMoveResponse>(r =>
                r.FailureIndexing &&
                r.TvShowId == showId &&
                r.TvShowName == showName &&
                r.EpisodeId == episodeId),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.InternalError(
            It.Is<ApiErrorResponse>(_ => true),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
