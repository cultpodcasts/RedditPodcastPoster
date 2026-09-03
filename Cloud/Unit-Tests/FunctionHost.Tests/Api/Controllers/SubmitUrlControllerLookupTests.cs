using System.Collections.Specialized;
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
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Controllers;

public class SubmitUrlControllerLookupTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SubmitUrlControllerLookupTests()
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

        _mocker.GetMock<IGetSubmitUrlLookupHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IHandlerContext ctx, Uri _, CancellationToken _) => ctx.Ok());
    }

    [Fact(DisplayName =
        "When Isolated GET has no url query, SubmitUrlController returns 400 and does not call the lookup handler, " +
        "because a missing query url must not be categorised.")]
    public async Task missing_query_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = CreateGetWithQuery(null);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        VerifyLookupNever();
    }

    [Fact(DisplayName =
        "When Isolated GET has an empty url query, SubmitUrlController returns 400 and does not call the lookup handler.")]
    public async Task empty_query_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = CreateGetWithQuery("");

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        VerifyLookupNever();
    }

    [Fact(DisplayName =
        "When Isolated GET has a relative lookup url, SubmitUrlController returns 400, " +
        "because lookup uses the same absolute http(s) gate as SubmitUrl.")]
    public async Task relative_query_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = CreateGetWithQuery("/foo");

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        VerifyLookupNever();
    }

    [Fact(DisplayName =
        "When Isolated GET has an ftp lookup url, SubmitUrlController returns 400, " +
        "because only http and https schemes are allowed.")]
    public async Task ftp_query_url_returns_400_without_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var (req, _) = CreateGetWithQuery("ftp://example.com/episode");

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInvalidUrlBody(result);
        VerifyLookupNever();
    }

    [Fact(DisplayName =
        "When Isolated GET has an absolute https lookup url, SubmitUrlController delegates to GetSubmitUrlLookupHandler.")]
    public async Task absolute_https_query_url_delegates_to_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var url = $"https://example.com/{_fixture.CreateGuid():N}";
        var (req, _) = CreateGetWithQuery(url);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.Is<Uri>(u => u.ToString() == url),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<IPostSubmitUrlHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<global::Api.Models.SubmitUrlRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When Isolated GET has an absolute http lookup url, SubmitUrlController delegates, " +
        "because http is an allowed submit scheme.")]
    public async Task absolute_http_query_url_delegates_to_handler()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SubmitUrlController>();
        var url = $"http://example.com/{_fixture.CreateGuid():N}";
        var (req, _) = CreateGetWithQuery(url);

        // Act
        var result = await sut.Get(req.Object, req.Object.FunctionContext, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (Mock<HttpRequestData> Req, Mock<HttpResponseData> Response) CreateGetWithQuery(string? url)
    {
        var (req, response) = HttpTestHelpers.CreateRequestResponse("GET");
        var query = new NameValueCollection();
        if (url != null)
        {
            query["url"] = url;
        }

        req.Setup(r => r.Query).Returns(query);
        return (req, response);
    }

    private void VerifyLookupNever()
    {
        _mocker.GetMock<IGetSubmitUrlLookupHandler>().Verify(
            h => h.Handle(It.IsAny<IHandlerContext>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
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
