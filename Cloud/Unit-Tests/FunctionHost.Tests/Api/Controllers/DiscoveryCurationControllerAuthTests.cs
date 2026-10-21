using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class DiscoveryCurationControllerAuthTests
{
    private readonly AutoMocker _mocker = new();
    private ClientPrincipal? _principal;

    public DiscoveryCurationControllerAuthTests()
    {
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(() => _principal);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
        _mocker.GetMock<IPostDiscoveryCurationHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<global::Api.Models.DiscoverySubmitRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, global::Api.Models.DiscoverySubmitRequest _, CancellationToken _) =>
                ctx.Accepted());
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery curation POST admits a curate:supervised-only caller, because supervised agents must reach the handler that holds their submit for review.")]
    public async Task post_admits_supervised_curator()
    {
        // Arrange
        UsePermission("curate:supervised");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryCurationController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.Post(req.Object, null!, new global::Api.Dtos.DiscoverySubmitRequest(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact(DisplayName =
        "INTEGRITY: Discovery curation POST rejects a caller with neither curate nor curate:supervised, because discovery submit is a curator action.")]
    public async Task post_rejects_without_curate_permissions()
    {
        // Arrange
        UsePermission("submit");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryCurationController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.Post(req.Object, null!, new global::Api.Dtos.DiscoverySubmitRequest(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IPostDiscoveryCurationHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<global::Api.Models.DiscoverySubmitRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "INTEGRITY: A supervised curator holding curate and curate:supervised can load discovery reports, because supervised users must also hold curate (only discovery submit is supervised).")]
    public async Task get_allows_curate_plus_supervised()
    {
        // Arrange
        UsePermission("curate", "curate:supervised");
        _mocker.GetMock<IGetDiscoveryCurationHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, CancellationToken _) => ctx.Ok());
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryCurationController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "INTEGRITY: curate:supervised alone does not grant discovery GET, because the documented role combination is curate plus curate:supervised.")]
    public async Task get_rejects_supervised_only()
    {
        // Arrange
        UsePermission("curate:supervised");
        var sut = _mocker.CreateInstance<global::Api.Controllers.DiscoveryCurationController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.Get(req.Object, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private void UsePermission(params string[] permissions)
    {
        _principal = new ClientPrincipal
        {
            Claims = permissions.Select(p => new ClientPrincipalClaim { Type = "permissions", Value = p }).ToArray()
        };
    }
}
