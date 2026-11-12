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

/// <summary>#1397: status codes, bodies and the redirect query of DELETE newsorganisation/{id}.</summary>
public class NewsOrganisationDeleteHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private readonly Mock<INewsOrganisationDeleteService> _service;

    public NewsOrganisationDeleteHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.BadRequest(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _service = _mocker.GetMock<INewsOrganisationDeleteService>();
    }

    private static object? Property(object body, string name) => body.GetType().GetProperty(name)?.GetValue(body);

    private void DeleteReturns(NewsOrganisationDeleteResult result) =>
        _service.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact(DisplayName =
        "Deleting an organisation that still has reports responds 409 with the remaining count, one being indexed 409 with a message only, and an empty one 200.")]
    public async Task delete_maps_has_reports_and_busy_to_409_and_deleted_to_200()
    {
        // Arrange
        var remaining = _fixture.Create<int>() % 50 + 1;
        var sut = _mocker.CreateInstance<DeleteNewsOrganisationHandler>();

        // Act
        DeleteReturns(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.HasReports, remaining));
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);
        DeleteReturns(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Busy));
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);
        DeleteReturns(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Deleted));
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<object>(body => Equals(Property(body, "remainingReportCount"), remaining)),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.Conflict(
            It.Is<object>(body => Property(body, "remainingReportCount") == null && Property(body, "message") != null),
            It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.Ok(), Times.Once);
    }

    [Fact(DisplayName =
        "The redirectToNewsOrganisationId query is passed to the service as a guid, and a value that is not a guid is a 400 without calling the service.")]
    public async Task redirect_query_is_parsed_or_refused()
    {
        // Arrange
        var organisationId = _fixture.CreateGuid();
        var redirectTo = _fixture.CreateGuid();
        DeleteReturns(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Deleted));
        var sut = _mocker.CreateInstance<DeleteNewsOrganisationHandler>();

        // Act
        _ctx.Setup(c => c.Query(DeleteNewsOrganisationHandler.RedirectQueryParameter)).Returns(redirectTo.ToString());
        await sut.Handle(_ctx.Object, organisationId, CancellationToken.None);
        _ctx.Setup(c => c.Query(DeleteNewsOrganisationHandler.RedirectQueryParameter)).Returns($"not-a-guid-{_fixture.Create<int>()}");
        await sut.Handle(_ctx.Object, organisationId, CancellationToken.None);

        // Assert
        _service.Verify(s => s.DeleteAsync(organisationId, redirectTo, It.IsAny<CancellationToken>()), Times.Once);
        _service.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        _ctx.Verify(x => x.BadRequest(It.IsAny<ApiErrorResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "A bad or unsafe redirect target from the service responds 400 with its error.")]
    public async Task bad_redirect_maps_to_400()
    {
        // Arrange
        DeleteReturns(new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.BadRequest)
            { Error = _fixture.Create<string>() });
        var sut = _mocker.CreateInstance<DeleteNewsOrganisationHandler>();

        // Act
        await sut.Handle(_ctx.Object, _fixture.CreateGuid(), CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.BadRequest(It.IsAny<ApiErrorResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}