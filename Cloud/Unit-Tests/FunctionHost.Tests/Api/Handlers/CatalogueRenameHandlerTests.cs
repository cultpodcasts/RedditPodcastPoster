using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Catalogue;
using Api.Models;
using Api.Services.Catalogue;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class CatalogueRenameHandlerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private CatalogueRenameResult _result = new(CatalogueRenameStatus.Ok,
        new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

    public CatalogueRenameHandlerTests()
    {
        var service = _mocker.GetMock<ICatalogueRenameService>();
        service.Setup(s => s.RenameNewsOrganisation(It.IsAny<CatalogueRenameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        service.Setup(s => s.RenameTvShow(It.IsAny<CatalogueRenameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        service.Setup(s => s.RenameFilm(It.IsAny<CatalogueRenameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _ctx = _mocker.GetMock<IHandlerContext>();
    }

    private Task Handle(string kind)
    {
        var sut = _mocker.CreateInstance<CatalogueRenameHandler>();
        var command = new CatalogueRenameCommand("Synthetic", "Synthetic Renamed");
        return kind switch
        {
            "news" => sut.HandleNewsOrganisation(_ctx.Object, command, CancellationToken.None),
            "tv" => sut.HandleTvShow(_ctx.Object, command, CancellationToken.None),
            _ => sut.HandleFilm(_ctx.Object, command, CancellationToken.None)
        };
    }

    [Theory(DisplayName = "A successful rename returns 200 with {indexState}, the same body as the podcast rename.")]
    [InlineData("news")]
    [InlineData("tv")]
    [InlineData("film")]
    public async Task ok_returns_index_state(string kind)
    {
        // Act
        await Handle(kind);

        // Assert
        _ctx.Verify(x => x.Ok(
            It.Is<CatalogueRenameResponse>(r => r.IndexState == SearchIndexerState.Executed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Status mapping follows RenamePodcastHandler: Conflict 409, NotFound 404, BadRequest 400, InvalidName 500.")]
    public async Task status_mapping()
    {
        foreach (var status in new[] { CatalogueRenameStatus.Conflict, CatalogueRenameStatus.NotFound,
                     CatalogueRenameStatus.BadRequest, CatalogueRenameStatus.InvalidName })
        {
            _result = new CatalogueRenameResult(status);
            await Handle("tv");
        }

        _ctx.Verify(x => x.Conflict(), Times.Once);
        _ctx.Verify(x => x.NotFound(), Times.Once);
        _ctx.Verify(x => x.BadRequest(), Times.Once);
        _ctx.Verify(x => x.InternalError(), Times.Once);
    }

    [Fact(DisplayName = "An ambiguous name is a 409 whose body is the candidate ids, like the kind's GET.")]
    public async Task ambiguous_conflict_has_ids()
    {
        // Arrange
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        _result = new CatalogueRenameResult(CatalogueRenameStatus.Conflict, AmbiguousIds: ids);

        // Act
        await Handle("film");

        // Assert
        _ctx.Verify(x => x.Conflict(ids, It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.Conflict(), Times.Never);
    }

    [Fact(DisplayName = "A failure returns 500 with an ApiErrorResponse naming the kind.")]
    public async Task failure_is_500()
    {
        // Arrange
        _result = new CatalogueRenameResult(CatalogueRenameStatus.Failed);

        // Act
        await Handle("news");

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<ApiErrorResponse>(r => r.Error.Contains("news organisation")), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
