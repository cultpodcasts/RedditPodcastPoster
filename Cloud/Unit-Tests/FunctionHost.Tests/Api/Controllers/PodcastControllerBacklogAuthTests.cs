using System.Net;
using Api.Configuration;
using Api.Controllers;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Azure.Diagnostics;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

/// <summary>
/// The backlog index walks the whole back catalogue and spends provider quota, so it and its preview are admin only
/// (cultpodcasts/RedditPodcastPoster#1433). The default hub index stays curator.
/// </summary>
public class PodcastControllerBacklogAuthTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private ClientPrincipal? _principal;

    public PodcastControllerBacklogAuthTests()
    {
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(() => _principal);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
        _mocker.GetMock<IIndexPodcastHandler>()
            .Setup(h => h.HandleBacklog(It.IsAny<IHandlerContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, string _, CancellationToken _) => ctx.Ok());
        _mocker.GetMock<IIndexPodcastHandler>()
            .Setup(h => h.HandleBacklogPreview(It.IsAny<IHandlerContext>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, string _, CancellationToken _) => ctx.Ok());
        _mocker.GetMock<IIndexPodcastHandler>()
            .Setup(h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, string _, CancellationToken _) => ctx.Ok());
    }

    [Fact(DisplayName =
        "When POST backlog index is called with an admin JWT, HandleRequest authorises and calls the backlog handler, because only an admin may walk the whole back catalogue.")]
    public async Task backlog_index_accepts_admin()
    {
        // Arrange
        UsePermission("admin");
        var sut = _mocker.CreateInstance<PodcastController>();
        var podcastName = _fixture.CreateTitle();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.IndexBacklog(req.Object, podcastName, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IIndexPodcastHandler>().Verify(
            h => h.HandleBacklog(It.IsAny<IHandlerContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When POST backlog index is called with a curate-only JWT, HandleRequest rejects it and does not call the backlog handler, because the backlog index is admin only.")]
    public async Task backlog_index_rejects_curate_only()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<PodcastController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.IndexBacklog(req.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IIndexPodcastHandler>().Verify(
            h => h.HandleBacklog(It.IsAny<IHandlerContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When GET backlog preview is called with an admin JWT, HandleRequest authorises and calls the preview handler, because the admin confirms the count before indexing.")]
    public async Task backlog_preview_accepts_admin()
    {
        // Arrange
        UsePermission("admin");
        var sut = _mocker.CreateInstance<PodcastController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.PreviewIndexBacklog(req.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IIndexPodcastHandler>().Verify(
            h => h.HandleBacklogPreview(It.IsAny<IHandlerContext>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When GET backlog preview is called with a curate-only JWT, HandleRequest rejects it and does not call the preview handler, because the preview is admin only like the index it confirms.")]
    public async Task backlog_preview_rejects_curate_only()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<PodcastController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await sut.PreviewIndexBacklog(req.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IIndexPodcastHandler>().Verify(
            h => h.HandleBacklogPreview(It.IsAny<IHandlerContext>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When POST hub index is called with a curate-only JWT, HandleRequest still authorises and calls the index handler, because only the backlog index became admin only.")]
    public async Task default_index_still_accepts_curate()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<PodcastController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await sut.Index(req.Object, _fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IIndexPodcastHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void UsePermission(string permission)
    {
        _principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = permission }]
        };
    }
}
