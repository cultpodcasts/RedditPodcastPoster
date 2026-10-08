using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.DiscoverySchedule;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class DiscoveryScheduleControllerAuthTests
{
    private readonly AutoMocker _mocker = new();
    private ClientPrincipal? _principal;

    public DiscoveryScheduleControllerAuthTests()
    {
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(() => _principal);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule GET requires admin permission, because UI and Worker gate this as Admin-only (not curate).")]
    public async Task get_requires_admin_rejects_curate_only()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryScheduleController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IGetDiscoveryScheduleHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule GET succeeds with admin permission, aligning with Worker permission admin.")]
    public async Task get_allows_admin()
    {
        // Arrange
        UsePermission("admin");
        _mocker.GetMock<IGetDiscoveryScheduleHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, CancellationToken _) => ctx.Ok());
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryScheduleController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IGetDiscoveryScheduleHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule PUT requires admin permission, because schedule mutation is Admin-only.")]
    public async Task put_requires_admin_rejects_curate_only()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryScheduleController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");
        var body = new global::Api.Dtos.DiscoveryScheduleUpdateRequest { RunTimes = ["08:00"] };

        // Act
        var result = await sut.Put(req.Object, null!, body, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IPutDiscoveryScheduleHandler>().Verify(
            h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<global::Api.Models.DiscoveryScheduleUpdateRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void UsePermission(string permission)
    {
        _principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = permission }]
        };
    }
}
