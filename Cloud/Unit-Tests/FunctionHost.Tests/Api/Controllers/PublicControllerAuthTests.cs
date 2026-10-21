using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
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
        // Arrange
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

        var controller = new global::Api.Controllers.PublicController(
            handler.Object,
            NullLogger<global::Api.Controllers.EpisodeController>.Instance,
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
        // Arrange
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync((ClientPrincipal?)null);

        var handler = new Mock<IGetPublicEpisodeHandler>(MockBehavior.Strict);

        var controller = new global::Api.Controllers.PublicController(
            handler.Object,
            NullLogger<global::Api.Controllers.EpisodeController>.Instance,
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            CreateMemoryProbeOrchestrator());

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var result = await controller.GetByEpisodeId(req.Object, Guid.NewGuid(), null!, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName =
        "Plain English rule: when the public podcast-episode route is called, then the podcast-id in the route reaches the handler as the podcast-id of the request and no podcast name, because the route carries a podcast-id and the episode must be found in that podcast.")]
    public async Task Public_podcast_episode_get_passes_route_podcast_id_to_handler()
    {
        // Arrange
        var podcastId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = "submit" }]
        };
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync(principal);

        PodcastEpisodeRequestWrapper? received = null;
        var handler = new Mock<IGetPublicEpisodeHandler>();
        handler.Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<PodcastEpisodeRequestWrapper>(),
                It.IsAny<CancellationToken>()))
            .Callback((IHandlerContext _, PodcastEpisodeRequestWrapper wrapper, CancellationToken _) =>
                received = wrapper)
            .ReturnsAsync((IHandlerContext ctx, PodcastEpisodeRequestWrapper _, CancellationToken _) => ctx.Ok());

        var controller = new global::Api.Controllers.PublicController(
            handler.Object,
            NullLogger<global::Api.Controllers.EpisodeController>.Instance,
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            CreateMemoryProbeOrchestrator());

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await controller.Get(req.Object, podcastId, episodeId, null!, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        received.Should().NotBeNull();
        received!.PodcastId.Should().Be(podcastId);
        received.PodcastName.Should().BeNull();
        received.EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "Plain English rule: when the public podcast-episode function is bound, then every non-infrastructure parameter is a placeholder of its route, because a parameter that is not in the route is never bound.")]
    public void Public_podcast_episode_get_parameters_are_all_route_placeholders()
    {
        // Arrange
        var method = typeof(global::Api.Controllers.PublicController)
            .GetMethod(nameof(global::Api.Controllers.PublicController.Get))!;
        var route = method.GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a != null)!.Route!;
        var placeholders = Regex.Matches(route, @"\{(?<name>\w+)(:[^}]*)?\}")
            .Select(m => m.Groups["name"].Value)
            .ToArray();

        // Act
        var bound = method.GetParameters()
            .Where(p => p.ParameterType != typeof(HttpRequestData) &&
                        p.ParameterType != typeof(FunctionContext) &&
                        p.ParameterType != typeof(CancellationToken))
            .Select(p => p.Name!)
            .ToArray();

        // Assert
        bound.Should().BeEquivalentTo(placeholders);
    }

    private static IMemoryProbeOrchestrator CreateMemoryProbeOrchestrator()
    {
        var orchestrator = new Mock<IMemoryProbeOrchestrator>();
        orchestrator.Setup(m => m.Start(It.IsAny<string>())).Returns(Mock.Of<IMemoryProbeScope>());
        return orchestrator.Object;
    }
}
