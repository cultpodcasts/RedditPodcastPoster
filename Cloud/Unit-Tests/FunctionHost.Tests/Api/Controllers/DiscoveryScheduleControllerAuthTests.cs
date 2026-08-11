using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.DiscoverySchedule;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class DiscoveryScheduleControllerAuthTests
{
    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule GET requires admin permission, because UI and Worker gate this as Admin-only (not curate).")]
    public async Task get_requires_admin_rejects_curate_only()
    {
        // Arrange
        var factory = PrincipalFactoryWithPermission("curate");
        var getHandler = new Mock<IGetDiscoveryScheduleHandler>(MockBehavior.Strict);
        var putHandler = new Mock<IPutDiscoveryScheduleHandler>(MockBehavior.Strict);
        var controller = CreateController(factory, getHandler, putHandler);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await controller.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule GET succeeds with admin permission, aligning with Worker permission admin.")]
    public async Task get_allows_admin()
    {
        // Arrange
        var factory = PrincipalFactoryWithPermission("admin");
        var getHandler = new Mock<IGetDiscoveryScheduleHandler>();
        getHandler
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, CancellationToken _) => ctx.Ok());
        var putHandler = new Mock<IPutDiscoveryScheduleHandler>(MockBehavior.Strict);
        var controller = CreateController(factory, getHandler, putHandler);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await controller.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        getHandler.Verify(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery Schedule PUT requires admin permission, because schedule mutation is Admin-only.")]
    public async Task put_requires_admin_rejects_curate_only()
    {
        // Arrange
        var factory = PrincipalFactoryWithPermission("curate");
        var getHandler = new Mock<IGetDiscoveryScheduleHandler>(MockBehavior.Strict);
        var putHandler = new Mock<IPutDiscoveryScheduleHandler>(MockBehavior.Strict);
        var controller = CreateController(factory, getHandler, putHandler);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");
        var body = new DiscoveryScheduleUpdateRequest { RunTimes = ["08:00"] };

        // Act
        var result = await controller.Put(req.Object, null!, body, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Mock<IClientPrincipalFactory> PrincipalFactoryWithPermission(string permission)
    {
        var principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = permission }]
        };
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync(principal);
        return factory;
    }

    private static global::Api.Controllers.DiscoveryScheduleController CreateController(
        Mock<IClientPrincipalFactory> factory,
        Mock<IGetDiscoveryScheduleHandler> getHandler,
        Mock<IPutDiscoveryScheduleHandler> putHandler) =>
        new(
            getHandler.Object,
            putHandler.Object,
            factory.Object,
            NullLogger<global::Api.Controllers.DiscoveryScheduleController>.Instance,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            CreateMemoryProbeOrchestrator());

    private static IMemoryProbeOrchestrator CreateMemoryProbeOrchestrator()
    {
        var orchestrator = new Mock<IMemoryProbeOrchestrator>();
        orchestrator.Setup(m => m.Start(It.IsAny<string>())).Returns(Mock.Of<IMemoryProbeScope>());
        return orchestrator.Object;
    }
}
