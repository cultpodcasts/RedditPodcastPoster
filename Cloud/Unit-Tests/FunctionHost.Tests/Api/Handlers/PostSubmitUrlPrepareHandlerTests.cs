using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;
using FunctionHost.Tests.Api;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace FunctionHost.Tests.Api.Handlers;

public class PostSubmitUrlPrepareHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact(DisplayName =
        "When prepare succeeds, POST SubmitUrl/prepare responds 200 with service in the JSON body " +
        "so Worker clients can cache the streaming ServiceKeys value.")]
    public async Task prepare_ok_returns_200_with_service()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var title = _fixture.CreateTitle();
        var showName = _fixture.CreateTitle();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.PrepareAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(
                SubmitUrlPrepareStatus.Ok,
                StreamingService.Itvx,
                new NonPodcastServiceItemMetaData(
                    Title: title,
                    Description: _fixture.Create<string>(),
                    ShowName: showName)));
        var handler = _mocker.CreateInstance<PostSubmitUrlPrepareHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlPrepareRequest { Url = url },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("service").GetString().Should().Be(StreamingServiceWire.ToKey(StreamingService.Itvx));
        body.GetProperty("title").GetString().Should().Be(title);
        body.GetProperty("podcastName").GetString().Should().Be(showName);
    }

    [Fact(DisplayName =
        "When extract succeeds, POST SubmitUrl/extract responds 200 with service in the JSON body " +
        "so Browser Rendering HTML can be mapped without a second Azure fetch.")]
    public async Task extract_ok_returns_200_with_service()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var html = _fixture.Create<string>();
        var title = _fixture.CreateTitle();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.ExtractAsync(url, html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(
                SubmitUrlPrepareStatus.Ok,
                StreamingService.Itvx,
                new NonPodcastServiceItemMetaData(
                    Title: title,
                    Description: _fixture.Create<string>())));
        var handler = _mocker.CreateInstance<PostSubmitUrlExtractHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlExtractRequest { Url = url, Html = html },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("service").GetString().Should().Be(StreamingServiceWire.ToKey(StreamingService.Itvx));
        body.GetProperty("title").GetString().Should().Be(title);
    }

    [Fact(DisplayName =
        "Plain English rule: when prepare is rejected, then POST SubmitUrl/prepare responds 400 with the error, because an unsupported URL is a client error.")]
    public async Task prepare_bad_request_returns_400()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var message = _fixture.Create<string>();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.PrepareAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(SubmitUrlPrepareStatus.BadRequest, Message: message));
        var handler = _mocker.CreateInstance<PostSubmitUrlPrepareHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlPrepareRequest { Url = url },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().Be(message);
    }

    [Fact(DisplayName =
        "Plain English rule: when prepare fails, then POST SubmitUrl/prepare responds 500 with the error, because an extract failure is not a client rejection.")]
    public async Task prepare_failed_returns_500()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var message = _fixture.Create<string>();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.PrepareAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(SubmitUrlPrepareStatus.Failed, Message: message));
        var handler = _mocker.CreateInstance<PostSubmitUrlPrepareHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlPrepareRequest { Url = url },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().Be(message);
    }

    [Fact(DisplayName =
        "Plain English rule: when extract is rejected, then POST SubmitUrl/extract responds 400 with the error, because an unsupported HTML path is a client error.")]
    public async Task extract_bad_request_returns_400()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var html = _fixture.Create<string>();
        var message = _fixture.Create<string>();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.ExtractAsync(url, html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(SubmitUrlPrepareStatus.BadRequest, Message: message));
        var handler = _mocker.CreateInstance<PostSubmitUrlExtractHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlExtractRequest { Url = url, Html = html },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().Be(message);
    }

    [Fact(DisplayName =
        "Plain English rule: when extract fails, then POST SubmitUrl/extract responds 500 with the error, because an HTML extract failure is not a client rejection.")]
    public async Task extract_failed_returns_500()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var html = _fixture.Create<string>();
        var message = _fixture.Create<string>();
        _mocker.GetMock<ISubmitUrlPrepareService>()
            .Setup(s => s.ExtractAsync(url, html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlPrepareResult(SubmitUrlPrepareStatus.Failed, Message: message));
        var handler = _mocker.CreateInstance<PostSubmitUrlExtractHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new global::Api.Models.SubmitUrlExtractRequest { Url = url, Html = html },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().Be(message);
    }
}
