using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class PublicControllerAuthTests
{
    [Fact(DisplayName = "Public episode get allows any authenticated principal (no curate role)")]
    public async Task Public_get_allows_authenticated_without_curate()
    {
        var principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = "submit" }]
        };
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync(principal);

        var handler = new Mock<IGetPublicEpisodeHandler>();
        handler.Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<PodcastEpisodeRequestWrapper>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, PodcastEpisodeRequestWrapper _, CancellationToken _) =>
                ctx.Ok());

        var controller = new global::Api.PublicController(
            handler.Object,
            NullLogger<global::Api.EpisodeController>.Instance,
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            CreateMemoryProbeOrchestrator());

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var result = await controller.GetByEpisodeId(req.Object, Guid.NewGuid(), null!, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Verify(h => h.Handle(
            It.IsAny<IHandlerContext>(),
            It.IsAny<PodcastEpisodeRequestWrapper>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Public episode get returns 401 when principal missing")]
    public async Task Public_get_requires_principal()
    {
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync((ClientPrincipal?)null);

        var handler = new Mock<IGetPublicEpisodeHandler>(MockBehavior.Strict);

        var controller = new global::Api.PublicController(
            handler.Object,
            NullLogger<global::Api.EpisodeController>.Instance,
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            CreateMemoryProbeOrchestrator());

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var result = await controller.GetByEpisodeId(req.Object, Guid.NewGuid(), null!, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static IMemoryProbeOrchestrator CreateMemoryProbeOrchestrator()
    {
        var orchestrator = new Mock<IMemoryProbeOrchestrator>();
        orchestrator.Setup(m => m.Start(It.IsAny<string>())).Returns(Mock.Of<IMemoryProbeScope>());
        return orchestrator.Object;
    }
}
