using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Films;
using Api.Models;
using Api.Services.Films;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using FilmCreateRequest = Api.Models.FilmCreateRequest;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// RPP #1413: <c>POST film</c> is a command. 202 carries the film id and name for the client to GET, never the film;
/// ambiguous identity is a 200 outcome; a clash is a 409 naming the existing film.
/// </summary>
public class PostFilmCreateHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public PostFilmCreateHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        foreach (var respond in new Action<Mock<IHandlerContext>>[]
                 {
                     c => c.Setup(x => x.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!),
                     c => c.Setup(x => x.Ok(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!),
                     c => c.Setup(x => x.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!),
                     c => c.Setup(x => x.NotFound(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!),
                     c => c.Setup(x => x.BadRequest(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!),
                     c => c.Setup(x => x.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((object _, CancellationToken _) => null!)
                 })
        {
            respond(_ctx);
        }
    }

    private async Task Handle(FilmCreateResult result)
    {
        _mocker.GetMock<IFilmCreateService>()
            .Setup(s => s.CreateAsync(It.IsAny<FilmCreateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var sut = _mocker.CreateInstance<PostFilmCreateHandler>();
        await sut.Handle(_ctx.Object, new FilmCreateCommand(new FilmCreateRequest()), CancellationToken.None);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the film is created, then Handle responds 202 with the film id and name only, because a command returns an acknowledgement and the client GETs the film.")]
    public async Task created_returns_202_with_id_and_name()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var name = _fixture.CreateTitle();

        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.Accepted, filmId, name));

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<FilmCreateResponse>(r => r.FilmId == filmId && r.Name == name && !r.FailureIndexing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the identity or name already belongs to a film, then Handle responds 409 with that film's id, because the client can use the existing film.")]
    public async Task conflict_returns_409_with_existing_film_id()
    {
        // Arrange
        var existingId = _fixture.CreateGuid();

        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.Conflict, existingId, _fixture.CreateTitle(), "clash"));

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<FilmCreateResponse>(r => r.FilmId == existingId && r.Error == "clash"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the identity is ambiguous, then Handle responds 200 with the candidates and no film id, because nothing was written.")]
    public async Task ambiguous_returns_200_with_candidates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = _fixture.CreateTmdbId();
        var hold = new CatalogueIdentityHold(
            CatalogueAuthorityCandidate.FilmKind,
            title,
            [
                new CatalogueAuthorityCandidate(
                    CatalogueAuthorityCandidate.TmdbKey(tmdbId), title, null,
                    CatalogueAuthorityCandidate.FilmKind, CatalogueAuthorityCandidate.TmdbSource, tmdbId, null)
            ]);

        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.IdentityDecisionRequired, IdentityHold: hold));

        // Assert
        _ctx.Verify(x => x.Ok(
            It.Is<FilmCreateResponse>(r =>
                r.FilmId == null &&
                r.IdentityDecision != null &&
                r.IdentityDecision.Candidates.Single().Key == CatalogueAuthorityCandidate.TmdbKey(tmdbId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the curator cancels, then Handle responds 200 with cancelled true.")]
    public async Task cancelled_returns_200_cancelled()
    {
        // Arrange
        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.Cancelled));

        // Assert
        _ctx.Verify(x => x.Ok(It.Is<FilmCreateResponse>(r => r.Cancelled), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when TMDB has no movie for the id, then Handle responds 404, because the identity does not exist.")]
    public async Task identity_not_found_returns_404()
    {
        // Arrange
        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.IdentityNotFound, Error: _fixture.CreateTitle()));

        // Assert
        _ctx.Verify(x => x.NotFound(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the request is invalid, then Handle responds 400 with the error.")]
    public async Task bad_request_returns_400()
    {
        // Arrange
        var error = _fixture.CreateTitle();

        // Act
        await Handle(new FilmCreateResult(FilmCreateStatus.BadRequest, Error: error));

        // Assert
        _ctx.Verify(x => x.BadRequest(
            It.Is<ApiErrorResponse>(r => r.Error == error),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostFilmCreateHandler: when the search push failed, then Handle responds 500 with the film DTO including failureIndexing, because the film exists and the client must know it is not indexed.")]
    public async Task failed_indexing_returns_500_with_failure_indexing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();

        // Act
        await Handle(new FilmCreateResult(
            FilmCreateStatus.Failed, filmId, _fixture.CreateTitle(), FailureIndexing: true));

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<FilmCreateResponse>(r => r.FailureIndexing && r.FilmId == filmId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
