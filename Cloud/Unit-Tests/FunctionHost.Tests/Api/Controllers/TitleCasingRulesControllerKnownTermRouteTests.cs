using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.TitleCasingRules;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Controllers;

public class TitleCasingRulesControllerKnownTermRouteTests
{
    private const string PercentLiteral = "%";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TitleCasingRulesControllerKnownTermRouteTests()
    {
        _mocker.GetMock<IOptions<HostingOptions>>()
            .Setup(o => o.Value)
            .Returns(new HostingOptions { TestMode = false });
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
        _mocker.GetMock<IClientPrincipalFactory>()
            .Setup(f => f.CreateAsync(It.IsAny<HttpRequestData>()))
            .ReturnsAsync(new ClientPrincipal
            {
                Claims = [new ClientPrincipalClaim { Type = "permissions", Value = "admin" }]
            });
    }

    [Fact(DisplayName =
        "Plain English rule: when a known-term literal contains a percent sign, then PUT passes that literal to the handler, because the Functions host already decoded the route value.")]
    public async Task put_known_term_passes_percent_literal_through()
    {
        // Arrange
        var language = _fixture.Create<string>();
        var pattern = _fixture.Create<string>();
        TitleCasingRulesLanguageKnownTermAdd? received = null;
        _mocker.GetMock<IPostTitleCasingRulesKnownTermHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesLanguageKnownTermAdd>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesLanguageKnownTermAdd, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesLanguageKnownTermAdd _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await controller.PutKnownTerm(
            req.Object,
            language,
            PercentLiteral,
            null!,
            new KnownTermUpsertBody { Pattern = pattern, Options = null },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Term.Literal.Should().Be(PercentLiteral);
        received.Term.Pattern.Should().Be(pattern);
        received.Term.Options.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when a known-term literal contains a percent sign, then DELETE passes that literal to the handler, because the Functions host already decoded the route value.")]
    public async Task delete_known_term_passes_percent_literal_through()
    {
        // Arrange
        var language = _fixture.Create<string>();
        TitleCasingRulesLanguageKnownTermDelete? received = null;
        _mocker.GetMock<IDeleteTitleCasingRulesKnownTermHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesLanguageKnownTermDelete>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesLanguageKnownTermDelete, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesLanguageKnownTermDelete _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await controller.DeleteKnownTerm(
            req.Object,
            language,
            PercentLiteral,
            null!,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Literal.Should().Be(PercentLiteral);
    }
}
