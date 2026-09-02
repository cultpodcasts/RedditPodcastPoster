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
        await AssertUrlRequiredBody(result);
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
        await AssertUrlRequiredBody(result);
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
        await AssertUrlRequiredBody(result);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated binds an absolute https url, SubmitUrlController delegates to PostSubmitUrlHandler, " +
        "because a present Url is the submit command.")]
    public async Task absolute_url_delegates_to_handler()
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

    private static async Task AssertUrlRequiredBody(HttpResponseData result)
    {
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.GetProperty("error").GetString().Should().Be("Url is required");
    }
}
