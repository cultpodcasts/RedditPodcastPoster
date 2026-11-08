using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using EpisodeTrailerAttachRequest = Api.Models.EpisodeTrailerAttachRequest;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// #1088: Trailer-attach handler maps service Conflict to HTTP 409 with the holding film's id
/// (docs/film-trailers.md). Failed/500 DTO wiring stays in <see cref="EpisodeMoveFailedHandlerRules"/>.
/// </summary>
public class PostEpisodeTrailerAttachHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public PostEpisodeTrailerAttachHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
    }

    [Fact(DisplayName =
        "PostEpisodeTrailerAttachHandler: when AttachAsync returns Conflict because another film holds the trailer, then Handle responds 409 with that film's id, because one episode cannot be a trailer of two films.")]
    public async Task trailer_attach_conflict_returns_409_with_holding_film_id()
    {
        // Arrange
        var holdingFilmId = _fixture.CreateGuid();
        _mocker.GetMock<IEpisodeTrailerAttachService>()
            .Setup(s => s.AttachAsync(It.IsAny<EpisodeTrailerAttachCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeTrailerAttachResult(
                EpisodeTrailerAttachStatus.Conflict,
                holdingFilmId,
                Error: "This episode is already a trailer of another film"));
        var command = new EpisodeTrailerAttachCommand(
            _fixture.CreateGuid(),
            _fixture.CreateGuid(),
            new EpisodeTrailerAttachRequest { FilmId = _fixture.CreateGuid() });
        var sut = _mocker.CreateInstance<PostEpisodeTrailerAttachHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<EpisodeTrailerAttachResponse>(r =>
                r.FilmId == holdingFilmId &&
                r.Error == "This episode is already a trailer of another film"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
