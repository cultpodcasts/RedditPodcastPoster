using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Configuration;
using Api.Dtos;
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

    [Fact(DisplayName =
        "Plain English rule: when a lower-case term contains a percent sign, then DELETE passes that term to the handler, because the Functions host already decoded the route value.")]
    public async Task delete_lower_case_term_passes_percent_term_through()
    {
        // Arrange
        var language = _fixture.Create<string>();
        TitleCasingRulesLanguageTerm? received = null;
        _mocker.GetMock<IDeleteTitleCasingRulesLowerCaseTermHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesLanguageTerm>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesLanguageTerm, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesLanguageTerm _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await controller.DeleteLowerCaseTerm(
            req.Object,
            language,
            PercentLiteral,
            null!,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Term.Should().Be(PercentLiteral);
    }

    [Fact(DisplayName =
        "Plain English rule: when an ignored subject contains a percent sign, then DELETE passes that term to the handler, because the Functions host already decoded the route value.")]
    public async Task delete_ignored_subject_passes_percent_term_through()
    {
        // Arrange
        var language = _fixture.Create<string>();
        TitleCasingRulesLanguageTerm? received = null;
        _mocker.GetMock<IDeleteTitleCasingRulesIgnoredSubjectHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesLanguageTerm>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesLanguageTerm, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesLanguageTerm _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await controller.DeleteIgnoredSubject(
            req.Object,
            language,
            PercentLiteral,
            null!,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Term.Should().Be(PercentLiteral);
    }

    [Fact(DisplayName =
        "Plain English rule: when a lower-case term is posted, then the handler receives the mapped model for that language, because the controller maps the body once.")]
    public async Task post_lower_case_term_maps_body_once()
    {
        // Arrange
        var language = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        TitleCasingRulesLowerCaseTermAdd? received = null;
        _mocker.GetMock<IPostTitleCasingRulesLowerCaseTermHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesLowerCaseTermAdd>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesLowerCaseTermAdd, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesLowerCaseTermAdd _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await controller.PostLowerCaseTerm(
            req.Object,
            language,
            null!,
            new global::Api.Dtos.TitleCasingRulesAddLowerCaseTermRequest { Term = term },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Term.Term.Should().Be(term);
    }

    [Fact(DisplayName =
        "Plain English rule: when an ignored subject is posted, then the handler receives the ignored-subject model for that language, because that command is separate from a lower-case term.")]
    public async Task post_ignored_subject_maps_its_own_model()
    {
        // Arrange
        var language = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        TitleCasingRulesIgnoredSubjectAdd? received = null;
        _mocker.GetMock<IPostTitleCasingRulesIgnoredSubjectHandler>()
            .Setup(h => h.Handle(
                It.IsAny<IHandlerContext>(),
                It.IsAny<TitleCasingRulesIgnoredSubjectAdd>(),
                It.IsAny<CancellationToken>()))
            .Callback<IHandlerContext, TitleCasingRulesIgnoredSubjectAdd, CancellationToken>((_, body, _) =>
                received = body)
            .Returns((IHandlerContext ctx, TitleCasingRulesIgnoredSubjectAdd _, CancellationToken _) =>
                Task.FromResult(ctx.Accepted()));
        var controller = _mocker.CreateInstance<global::Api.Controllers.TitleCasingRulesController>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await controller.PostIgnoredSubject(
            req.Object,
            language,
            null!,
            new global::Api.Dtos.TitleCasingRulesAddIgnoredSubjectRequest { Term = term },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        received.Should().NotBeNull();
        received!.Language.Should().Be(language);
        received.Term.Term.Should().Be(term);
    }
}
