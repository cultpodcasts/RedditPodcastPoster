using System.Collections.Specialized;
using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Configuration;
using Api.Controllers;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Controllers;

public class SubmitUrlControllerAuthTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private ClientPrincipal? _principal;

    public SubmitUrlControllerAuthTests()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<SubmitUrlController>>(
            NullLogger<SubmitUrlController>.Instance);
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(() => _principal);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
        _mocker.GetMock<IGetSubmitUrlLookupHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, Uri _, CancellationToken _) => ctx.Ok());
        _mocker.GetMock<IPostSubmitUrlHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<SubmitUrlRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, SubmitUrlRequest _, CancellationToken _) => ctx.Ok());
    }

    [Fact(DisplayName =
        "When Isolated GET SubmitUrl lookup is called with a curate JWT, HandleRequest authorises and calls the lookup handler, " +
        "because the Worker forwards Curator tokens with curate, not submit.")]
    public async Task get_lookup_accepts_curate()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var (req, _) = CreateGetWithQuery(url.AbsoluteUri);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), url, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated POST SubmitUrl is called with a curate JWT, HandleRequest authorises and calls the submit handler, " +
        "because the Worker forwards Curator tokens with curate, not submit.")]
    public async Task post_accepts_curate()
    {
        // Arrange
        UsePermission("curate");
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId()) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated GET SubmitUrl lookup has no principal, HandleRequest returns 401 and does not call the lookup handler, as today.")]
    public async Task get_lookup_rejects_unsigned()
    {
        // Arrange
        _principal = null;
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var (req, _) = CreateGetWithQuery(url.AbsoluteUri);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated POST SubmitUrl has no principal, HandleRequest returns 401 and does not call the submit handler, as today.")]
    public async Task post_rejects_unsigned()
    {
        // Arrange
        _principal = null;
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId()) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated GET SubmitUrl lookup is called with submit-only, HandleRequest still authorises, as today, " +
        "because Isolated accepts curate or submit.")]
    public async Task get_lookup_accepts_submit_only_as_today()
    {
        // Arrange
        UsePermission("submit");
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var (req, _) = CreateGetWithQuery(url.AbsoluteUri);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), url, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated POST SubmitUrl is called with submit-only, HandleRequest still authorises, as today, " +
        "because Isolated accepts curate or submit.")]
    public async Task post_accepts_submit_only_as_today()
    {
        // Arrange
        UsePermission("submit");
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId()) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void UsePermission(string permission)
    {
        _principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = permission }]
        };
    }

    private static (Mock<HttpRequestData> Req, Mock<HttpResponseData> Response) CreateGetWithQuery(string url)
    {
        var (req, response) = HttpTestHelpers.CreateRequestResponse("GET");
        req.Setup(r => r.Query).Returns(new NameValueCollection { ["url"] = url });
        return (req, response);
    }
}
