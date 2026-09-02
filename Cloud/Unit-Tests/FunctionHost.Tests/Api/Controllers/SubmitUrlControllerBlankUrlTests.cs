using System.Net;
using System.Text.Json;
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

public class SubmitUrlControllerBlankUrlTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SubmitUrlControllerBlankUrlTests()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<SubmitUrlController>>(
            NullLogger<SubmitUrlController>.Instance);
        _mocker.Use(Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }));

        var principal = new ClientPrincipal
        {
            Claims = [new ClientPrincipalClaim { Type = "permissions", Value = "submit" }]
        };
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(principal);

        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());

        _mocker.GetMock<IPostSubmitUrlHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<SubmitUrlRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, SubmitUrlRequest _, CancellationToken _) => ctx.Ok());
    }

    [Fact(DisplayName =
        "When Isolated binds url null, SubmitUrlController returns 400 and does not call the handler, " +
        "because a missing Uri must not be submitted.")]
    public async Task null_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = null! };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds url empty string as a relative empty Uri, SubmitUrlController returns 400 " +
        "and does not call the handler, because OriginalString is blank.")]
    public async Task empty_url_string_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri(string.Empty, UriKind.RelativeOrAbsolute) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds a whitespace-only url as a relative Uri, SubmitUrlController returns 400 " +
        "and does not call the handler, because OriginalString is whitespace.")]
    public async Task whitespace_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri("   ", UriKind.RelativeOrAbsolute) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute https URL, SubmitUrlController Handle delegates to PostSubmitUrlHandler, " +
        "because an absolute https URL is a usable submit URL.")]
    public async Task absolute_https_url_delegates_to_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.CreateGuid():N}")
        };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute http url, SubmitUrlController delegates to PostSubmitUrlHandler, " +
        "because http is an allowed submit scheme.")]
    public async Task absolute_http_url_delegates_to_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest
        {
            Url = new Uri($"http://example.com/{_fixture.CreateGuid():N}")
        };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute HTTP url with an uppercase scheme, SubmitUrlController Handle delegates, " +
        "because Uri.Scheme is already lowercase http and matches Uri.UriSchemeHttp.")]
    public async Task uppercase_http_scheme_delegates_to_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest
        {
            Url = new Uri($"HTTP://example.com/{_fixture.CreateGuid():N}")
        };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        model.Url.Scheme.Should().Be(Uri.UriSchemeHttp);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), model, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Isolated binds a relative path as Url, SubmitUrlController returns 400 and does not call the handler, " +
        "because submit requires an absolute http or https URL.")]
    public async Task relative_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri("/foo", UriKind.RelativeOrAbsolute) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds a non-URI string as a relative Url, SubmitUrlController returns 400 and does not call " +
        "the handler, because submit requires an absolute http or https URL.")]
    public async Task non_uri_string_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri("not-a-uri", UriKind.RelativeOrAbsolute) };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute ftp url, SubmitUrlController returns 400 and does not call the handler, " +
        "because only http and https schemes are allowed.")]
    public async Task ftp_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri("ftp://example.com/episode") };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute file url, SubmitUrlController returns 400 and does not call the handler, " +
        "because only http and https schemes are allowed.")]
    public async Task file_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var model = new SubmitUrlRequest { Url = new Uri("file:///tmp/episode") };

        // Act
        var result = await sut.Post(req.Object, req.Object.FunctionContext, model, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task AssertInvalidUrlBody(HttpResponseData result)
    {
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("error").GetString()
            .Should().Be("Url must be an absolute http or https URL");
    }
}
