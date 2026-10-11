using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using AutoFixture;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.DiscoverySubmissions;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class DiscoverySubmissionsControllerAuthTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private ClientPrincipal? _principal;

    public DiscoverySubmissionsControllerAuthTests()
    {
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(() => _principal);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
        _mocker.GetMock<IPostDiscoverySubmissionApplyHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(),
                It.IsAny<DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest> _,
                CancellationToken _) => ctx.Ok());
        _mocker.GetMock<IPutDiscoverySubmissionReviewHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(),
                It.IsAny<DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest> _,
                CancellationToken _) => ctx.Accepted());
        _mocker.GetMock<IGetDiscoverySubmissionsHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, CancellationToken _) => ctx.Ok());
    }

    [Theory(DisplayName =
        "Discovery submission apply rejects callers without supervise or admin (including curate:supervised and curate), because only a supervisor may promote held reports to Processed.")]
    [InlineData("curate:supervised")]
    [InlineData("curate")]
    public async Task apply_rejects_non_supervisors(string permission)
    {
        // Arrange
        UsePermission(permission);
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoverySubmissionsController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.PostApply(req.Object, _fixture.Create<Guid>(), null!,
            new global::Api.Dtos.DiscoverySubmissionApplyRequestDto(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IPostDiscoverySubmissionApplyHandler>().Verify(h => h.Handle(It.IsAny<IHandlerContext>(),
            It.IsAny<DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Discovery submission review save rejects a curate:supervised-only caller, because reviewing is a supervisor action.")]
    public async Task review_rejects_supervised_curator()
    {
        // Arrange
        UsePermission("curate:supervised");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoverySubmissionsController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await sut.PutReview(req.Object, _fixture.Create<Guid>(), null!,
            new global::Api.Dtos.DiscoverySubmissionReviewRequestDto(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory(DisplayName =
        "Discovery submission apply admits supervise or admin callers to the handler.")]
    [InlineData("supervise")]
    [InlineData("admin")]
    public async Task apply_admits_supervisors(string permission)
    {
        // Arrange
        UsePermission(permission);
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoverySubmissionsController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.PostApply(req.Object, _fixture.Create<Guid>(), null!,
            new global::Api.Dtos.DiscoverySubmissionApplyRequestDto(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Discovery submissions list admits a curate:supervised caller, because supervised curators see their own submissions and supervisor comments.")]
    public async Task list_admits_supervised_curator()
    {
        // Arrange
        UsePermission("curate:supervised");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoverySubmissionsController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.List(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private void UsePermission(string permission)
    {
        _principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = permission }]
        };
    }
}
