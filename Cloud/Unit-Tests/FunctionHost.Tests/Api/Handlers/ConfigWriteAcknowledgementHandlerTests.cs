using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.SupportedLanguages;
using Api.Handlers.TitleCasingRules;
using Api.Models;
using Api.Services.DiscoverySchedule;
using Api.Services.SupportedLanguages;
using Api.Services.TitleCasingRules;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class ConfigWriteAcknowledgementHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseData response)
    {
        var json = await ReadBodyAsync(response);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("error").GetString();
    }

    [Fact(DisplayName =
        "Plain English rule: when a discovery-schedule update is accepted, then respond 202 with an empty body, because a command acknowledges the write and the client loads the schedule with GET.")]
    public async Task discovery_schedule_update_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<IDiscoveryScheduleUpdateService>()
            .Setup(s => s.UpdateAsync(It.IsAny<DiscoveryScheduleUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryScheduleUpdateResult(DiscoveryScheduleUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<PutDiscoveryScheduleHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new DiscoveryScheduleUpdateRequest { RunTimes = [_fixture.Create<string>()] },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a discovery-schedule update is rejected, then respond 400 with the error message, because a failed command still returns the existing error shape.")]
    public async Task discovery_schedule_update_rejected_returns_400_with_error()
    {
        // Arrange
        var error = _fixture.Create<string>();
        _mocker.GetMock<IDiscoveryScheduleUpdateService>()
            .Setup(s => s.UpdateAsync(It.IsAny<DiscoveryScheduleUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryScheduleUpdateResult(DiscoveryScheduleUpdateStatus.BadRequest, error));
        var handler = _mocker.CreateInstance<PutDiscoveryScheduleHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new DiscoveryScheduleUpdateRequest { RunTimes = [_fixture.Create<string>()] },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(result)).Should().Be(error);
    }

    [Fact(DisplayName =
        "Plain English rule: when a supported language is added, then respond 202 with an empty body, because a command acknowledges the write and the client loads languages with GET.")]
    public async Task supported_language_add_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ISupportedLanguagesUpdateService>()
            .Setup(s => s.AddAsync(It.IsAny<SupportedLanguageAddRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<PostSupportedLanguagesHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SupportedLanguageAddRequest { Name = _fixture.Create<string>() },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a supported language is removed, then respond 202 with an empty body, because a command acknowledges the write and the client loads languages with GET.")]
    public async Task supported_language_delete_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ISupportedLanguagesUpdateService>()
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<DeleteSupportedLanguagesHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            _fixture.Create<string>(),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when adding a supported language is rejected, then respond 400 with the error message, because a failed command still returns the existing error shape.")]
    public async Task supported_language_add_rejected_returns_400_with_error()
    {
        // Arrange
        var error = _fixture.Create<string>();
        _mocker.GetMock<ISupportedLanguagesUpdateService>()
            .Setup(s => s.AddAsync(It.IsAny<SupportedLanguageAddRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.BadRequest, error));
        var handler = _mocker.CreateInstance<PostSupportedLanguagesHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SupportedLanguageAddRequest { Name = _fixture.Create<string>() },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(result)).Should().Be(error);
    }

    [Fact(DisplayName =
        "Plain English rule: when a lower-case term is added, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task lower_case_term_add_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.AddLowerCaseTermAsync(
                It.IsAny<string>(),
                It.IsAny<TitleCasingRulesAddLowerCaseTermRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<PostTitleCasingRulesLowerCaseTermHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageTerm(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a lower-case term is removed, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task lower_case_term_delete_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.DeleteLowerCaseTermAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<DeleteTitleCasingRulesLowerCaseTermHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageTerm(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a known term is saved, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task known_term_save_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.UpsertKnownTermAsync(
                It.IsAny<string>(),
                It.IsAny<KnownTermUpdate>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<PostTitleCasingRulesKnownTermHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageKnownTermAdd(
                _fixture.Create<string>(),
                new KnownTermUpdate
                {
                    Literal = _fixture.Create<string>(),
                    Pattern = _fixture.Create<string>()
                }),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a known term is removed, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task known_term_delete_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.DeleteKnownTermAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<DeleteTitleCasingRulesKnownTermHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageKnownTermDelete(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when an ignored subject is added, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task ignored_subject_add_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.AddIgnoredSubjectAsync(
                It.IsAny<string>(),
                It.IsAny<TitleCasingRulesAddLowerCaseTermRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<PostTitleCasingRulesIgnoredSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageTerm(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when an ignored subject is removed, then respond 202 with an empty body, because a command acknowledges the write and the client loads title-casing rules with GET.")]
    public async Task ignored_subject_delete_accepted_returns_202_with_empty_body()
    {
        // Arrange
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.DeleteIgnoredSubjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok));
        var handler = _mocker.CreateInstance<DeleteTitleCasingRulesIgnoredSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageTerm(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a title-casing write is rejected, then respond 400 with the error message, because a failed command still returns the existing error shape.")]
    public async Task title_casing_write_rejected_returns_400_with_error()
    {
        // Arrange
        var error = _fixture.Create<string>();
        _mocker.GetMock<ITitleCasingRulesUpdateService>()
            .Setup(s => s.AddLowerCaseTermAsync(
                It.IsAny<string>(),
                It.IsAny<TitleCasingRulesAddLowerCaseTermRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.BadRequest, error));
        var handler = _mocker.CreateInstance<PostTitleCasingRulesLowerCaseTermHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TitleCasingRulesLanguageTerm(_fixture.Create<string>(), _fixture.Create<string>()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(result)).Should().Be(error);
    }
}
