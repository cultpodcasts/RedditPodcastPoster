using System.Net;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Catalogue;
using Api.Models;
using Api.Services.Catalogue;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class CatalogueIndexHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private CatalogueIndexResult _result = new(CatalogueIndexStatus.Ok, [],
        new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

    public CatalogueIndexHandlerTests()
    {
        var service = _mocker.GetMock<ICatalogueParentIndexService>();
        service.Setup(s => s.IndexTvShow(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        service.Setup(s => s.IndexNewsOrganisation(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        _ctx = _mocker.GetMock<IHandlerContext>();
    }

    private Task Handle(string kind)
    {
        var sut = _mocker.CreateInstance<CatalogueIndexHandler>();
        var identifier = _fixture.CreateTitle();
        return kind == "tv"
            ? sut.HandleTvShow(_ctx.Object, identifier, CancellationToken.None)
            : sut.HandleNewsOrganisation(_ctx.Object, identifier, CancellationToken.None);
    }

    [Theory(DisplayName =
        "A successful index returns 200 with the indexed item ids and the search state, like the podcast index.")]
    [InlineData("tv")]
    [InlineData("news")]
    public async Task ok_returns_ids_and_search_state(string kind)
    {
        // Arrange
        var ids = new[] { _fixture.CreateGuid(), _fixture.CreateGuid() };
        _result = new CatalogueIndexResult(CatalogueIndexStatus.Ok, ids,
            new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        // Act
        await Handle(kind);

        // Assert
        _ctx.Verify(x => x.Ok(
            It.Is<CatalogueIndexResponse>(r =>
                r.IndexedItemIds.SequenceEqual(ids) && r.SearchIndexerState == SearchIndexerState.Executed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "An unknown TV show or news organisation is a 404.")]
    public async Task not_found_is_404()
    {
        // Arrange
        _result = new CatalogueIndexResult(CatalogueIndexStatus.NotFound);

        // Act
        await Handle("tv");
        await Handle("news");

        // Assert
        _ctx.Verify(x => x.NotFound(), Times.Exactly(2));
    }

    [Theory(DisplayName =
        "A refused index is a 400 whose body says why, so the hub page can say it is not indexable.")]
    [InlineData(CatalogueIndexStatus.NotIndexable, CatalogueIndexRejection.NotAutoIndexed)]
    [InlineData(CatalogueIndexStatus.SourcePodcastStillExists, CatalogueIndexRejection.SourcePodcastStillExists)]
    public async Task refused_is_400_with_reason(CatalogueIndexStatus status, string reason)
    {
        // Arrange
        _result = new CatalogueIndexResult(status);

        // Act
        await Handle("news");

        // Assert
        _ctx.Verify(x => x.BadRequest(
            It.Is<CatalogueIndexRejection>(r => r.Reason == reason), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "An ambiguous name is a 409 whose body is the candidate ids, like rename.")]
    public async Task ambiguous_is_409_with_ids()
    {
        // Arrange
        var ids = new[] { _fixture.CreateGuid(), _fixture.CreateGuid() };
        _result = new CatalogueIndexResult(CatalogueIndexStatus.Conflict, AmbiguousIds: ids);

        // Act
        await Handle("tv");

        // Assert
        _ctx.Verify(x => x.Conflict(It.Is<Guid[]>(body => body.SequenceEqual(ids)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "A failed index is a 500 with an error body.")]
    public async Task failure_is_500()
    {
        // Arrange
        _result = new CatalogueIndexResult(CatalogueIndexStatus.Failed);

        // Act
        await Handle("tv");

        // Assert
        _ctx.Verify(x => x.InternalError(It.IsAny<ApiErrorResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "A timed-out index is a 504 { reason: TimedOut } with a message that makes no claim about what was saved, " +
        "not a 500, because running out of time is not a fault and the hub page says to try again later.")]
    [InlineData("tv")]
    [InlineData("news")]
    public async Task timed_out_is_504_with_reason(string kind)
    {
        // Arrange
        _result = new CatalogueIndexResult(CatalogueIndexStatus.TimedOut);

        // Act
        await Handle(kind);

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
