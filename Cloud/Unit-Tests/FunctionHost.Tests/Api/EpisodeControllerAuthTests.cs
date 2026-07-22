using System.Net;
using Azure.Core.Serialization;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Api.Configuration;
using Api.Factories;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Api;

public class EpisodeControllerAuthTests
{
    [Fact(DisplayName = "Missing principal yields 401 Unauthorized for curated episode routes")]
    public async Task Missing_principal_returns_unauthorized()
    {
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync((ClientPrincipal?)null);

        var sut = new TestHttpFunction(
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            NullLogger.Instance);

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var result = await sut.Invoke(req.Object, ["curate"]);

        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Principal with curate scope is authorised")]
    public async Task Curate_scope_is_authorised()
    {
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "permissions", Value = "curate" }
            ]
        };
        var factory = new Mock<IClientPrincipalFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>())).ReturnsAsync(principal);

        var sut = new TestHttpFunction(
            factory.Object,
            Options.Create(new HostingOptions { TestMode = false, UserRoles = [] }),
            NullLogger.Instance);

        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var result = await sut.Invoke(req.Object, ["curate"]);

        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private sealed class TestHttpFunction(
        IClientPrincipalFactory clientPrincipalFactory,
        IOptions<HostingOptions> hostingOptions,
        Microsoft.Extensions.Logging.ILogger logger)
        : global::Api.BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
    {
        public Task<HttpResponseData> Invoke(HttpRequestData req, string[] roles) =>
            HandleRequest(
                req,
                roles,
                (_, _) => Task.FromResult(req.CreateResponse(HttpStatusCode.Accepted)),
                Unauthorised,
                CancellationToken.None);
    }
}

internal static class HttpTestHelpers
{
    public static (Mock<HttpRequestData> Req, Mock<HttpResponseData> Response) CreateRequestResponse(
        string method = "DELETE")
    {
        var services = new ServiceCollection();
        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer();
        });
        var serviceProvider = services.BuildServiceProvider();

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.InstanceServices).Returns(serviceProvider);
        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.SetupGet(d => d.Name).Returns("TestFunction");
        context.SetupGet(c => c.FunctionDefinition).Returns(functionDefinition.Object);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var response = new Mock<HttpResponseData>(context.Object);
        response.SetupProperty(r => r.StatusCode, HttpStatusCode.OK);
        response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
        response.Setup(r => r.Body).Returns(new MemoryStream());

        var req = new Mock<HttpRequestData>(context.Object);
        req.Setup(r => r.CreateResponse()).Returns(response.Object);
        req.Setup(r => r.Url).Returns(new Uri("https://localhost/api/episode/test"));
        req.Setup(r => r.Method).Returns(method);
        req.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        req.Setup(r => r.Body).Returns(new MemoryStream());

        return (req, response);
    }
}
