using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using FunctionHost.Tests.Api;

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
                new SubmitUrlPrepareResponse
                {
                    Service = ServiceKeys.Itvx,
                    PodcastName = showName,
                    Title = title,
                    Description = _fixture.Create<string>(),
                    ShowName = showName
                }));
        var handler = _mocker.CreateInstance<PostSubmitUrlPrepareHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlPrepareRequest { Url = url },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("service").GetString().Should().Be(ServiceKeys.Itvx);
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
                new SubmitUrlPrepareResponse
                {
                    Service = ServiceKeys.Itvx,
                    Title = title,
                    Description = _fixture.Create<string>()
                }));
        var handler = _mocker.CreateInstance<PostSubmitUrlExtractHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlExtractRequest { Url = url, Html = html },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("service").GetString().Should().Be(ServiceKeys.Itvx);
        body.GetProperty("title").GetString().Should().Be(title);
    }
}
