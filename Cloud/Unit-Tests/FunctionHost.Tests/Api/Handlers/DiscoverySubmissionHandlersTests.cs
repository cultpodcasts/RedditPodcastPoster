using System.Net;
using System.Security.Claims;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.DiscoverySubmissions;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class DiscoverySubmissionHandlersTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private SupervisionCaller? _caller;

    public DiscoverySubmissionHandlersTests()
    {
        _mocker.GetMock<IDiscoverySubmissionReviewService>()
            .Setup(s => s.Apply(It.IsAny<Guid>(), It.IsAny<DiscoverySubmissionApplyRequest>(),
                It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, DiscoverySubmissionApplyRequest _, SupervisionCaller c, CancellationToken _) => _caller = c)
            .ReturnsAsync(new DiscoverySubmissionCommandResult(DiscoverySubmissionCommandStatus.Ok));
        _mocker.GetMock<IDiscoverySubmissionReviewService>()
            .Setup(s => s.SaveReview(It.IsAny<Guid>(), It.IsAny<DiscoverySubmissionReviewRequest>(),
                It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySubmissionCommandResult(DiscoverySubmissionCommandStatus.Ok));
        _mocker.GetMock<IDiscoverySubmissionReviewService>()
            .Setup(s => s.List(It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()))
            .Callback((SupervisionCaller c, CancellationToken _) => _caller = c)
            .ReturnsAsync(new DiscoverySubmissionQueryResult<DiscoverySubmissionList>(DiscoverySubmissionQueryStatus.Ok,
                new DiscoverySubmissionList([], 0, false)));
    }

    [Theory(DisplayName =
        "INVARIANT: A curate:supervised caller is forbidden from applying a submission even when also granted supervise or admin, because a supervised curator must never promote reports to Processed.")]
    [InlineData("curate:supervised", "supervise")]
    [InlineData("curate:supervised", "admin")]
    [InlineData("curate:supervised", "supervise", "admin", "curate")]
    public async Task supervised_caller_cannot_apply(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionApplyHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionApplyRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.Apply(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionApplyRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "A curate:supervised caller is forbidden from saving a supervisor review, because reviewing is a supervisor action.")]
    [InlineData("curate:supervised")]
    [InlineData("curate:supervised", "supervise")]
    public async Task supervised_caller_cannot_review(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PutDiscoverySubmissionReviewHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionReviewRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.SaveReview(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionReviewRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A supervise caller's apply reaches the review service as a supervisor and a successful apply returns 200 with the command outcome.")]
    public async Task supervisor_apply_reaches_service()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionApplyHandler>();

        // Act
        var result = await sut.Handle(Context("supervise"),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionApplyRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _caller!.IsSupervisor.Should().BeTrue();
        _caller.IsAdmin.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A curate:supervised caller listing submissions is treated as a submitter (their own submissions), never as a supervisor.")]
    public async Task supervised_caller_lists_as_submitter()
    {
        // Arrange
        var sut = _mocker.CreateInstance<GetDiscoverySubmissionsHandler>();

        // Act
        var result = await sut.Handle(Context("curate:supervised", "supervise"), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _caller!.CanSuperviseAny.Should().BeFalse();
    }

    [Theory(DisplayName =
        "INVARIANT: A curate:supervised caller is forbidden from retrying a submission even when also granted supervise or admin, because retry ingests episodes.")]
    [InlineData("curate:supervised", "supervise")]
    [InlineData("curate:supervised", "admin")]
    public async Task supervised_caller_cannot_retry(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionRetryHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionRetryRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionRetryRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.Retry(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionRetryRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "A curate:supervised caller is forbidden from releasing a submission even when also granted supervise or admin.")]
    [InlineData("curate:supervised", "supervise")]
    [InlineData("curate:supervised", "admin")]
    public async Task supervised_caller_cannot_release(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionReleaseHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionReleaseRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionReleaseRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.Release(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionReleaseRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "Releasing a stuck report is admin-only: a supervisor, or a supervised curator holding admin, is forbidden.")]
    [InlineData("supervise")]
    [InlineData("curate:supervised", "admin")]
    public async Task stuck_report_release_admin_only(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoveryReportReleaseHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoveryReportReleaseRequest>(_fixture.Create<Guid>(),
                new DiscoveryReportReleaseRequest()), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.ReleaseStuckReport(It.IsAny<Guid>(),
            It.IsAny<DiscoveryReportReleaseRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "Recovering a stuck Applying submission is admin-only: a supervisor, or a supervised curator holding admin, is forbidden.")]
    [InlineData("supervise")]
    [InlineData("curate:supervised", "admin")]
    public async Task recover_admin_only(params string[] permissions)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionRecoverHandler>();

        // Act
        var result = await sut.Handle(Context(permissions),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionRecoverRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionRecoverRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.Recover(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionRecoverRequest>(), It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "An admin's recover request reaches the review service.")]
    public async Task admin_recover_reaches_service()
    {
        // Arrange
        _mocker.GetMock<IDiscoverySubmissionReviewService>()
            .Setup(s => s.Recover(It.IsAny<Guid>(), It.IsAny<DiscoverySubmissionRecoverRequest>(),
                It.IsAny<SupervisionCaller>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySubmissionCommandResult(DiscoverySubmissionCommandStatus.Ok));
        var sut = _mocker.CreateInstance<PostDiscoverySubmissionRecoverHandler>();

        // Act
        var result = await sut.Handle(Context("admin"),
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionRecoverRequest>(_fixture.Create<Guid>(),
                new DiscoverySubmissionRecoverRequest { ETag = _fixture.Create<string>() }), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IDiscoverySubmissionReviewService>().Verify(s => s.Recover(It.IsAny<Guid>(),
            It.IsAny<DiscoverySubmissionRecoverRequest>(), It.Is<SupervisionCaller>(x => x.IsAdmin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private IHandlerContext Context(params string[] permissions)
    {
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var principal = new ClientPrincipal
        {
            Claims = permissions.Select(p => new ClientPrincipalClaim { Type = "permissions", Value = p })
                .Append(new ClientPrincipalClaim { Type = ClaimTypes.NameIdentifier, Value = _fixture.Create<string>() })
                .ToArray()
        };
        return new HandlerContext(req.Object, principal);
    }
}
