using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.TitleCasingRules;
using Api.Models;
using Api.Services.TitleCasingRules;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TitleCasing;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Handlers;

public class GetTitleCasingRulesByLanguageHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules succeeds, then respond 200, because the client loads the language document.")]
    public async Task ok_returns_200()
    {
        // Arrange
        var language = "es";
        _mocker.GetMock<ITitleCasingRulesGetService>()
            .Setup(s => s.GetAsync(language, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesGetResult(
                TitleCasingRulesGetStatus.Ok,
                new NonEnglishTitleCasingRulesDocument(language)));
        var handler = _mocker.CreateInstance<GetTitleCasingRulesByLanguageHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), language, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules finds no language, then respond 404, because a missing non-English document is not an empty default.")]
    public async Task not_found_returns_404()
    {
        // Arrange
        var language = _fixture.Create<string>();
        _mocker.GetMock<ITitleCasingRulesGetService>()
            .Setup(s => s.GetAsync(language, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesGetResult(TitleCasingRulesGetStatus.NotFound));
        var handler = _mocker.CreateInstance<GetTitleCasingRulesByLanguageHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), language, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules fails, then respond 500, because a read error is not a missing language.")]
    public async Task failed_returns_500()
    {
        // Arrange
        var language = _fixture.Create<string>();
        _mocker.GetMock<ITitleCasingRulesGetService>()
            .Setup(s => s.GetAsync(language, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesGetResult(TitleCasingRulesGetStatus.Failed));
        var handler = _mocker.CreateInstance<GetTitleCasingRulesByLanguageHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), language, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
