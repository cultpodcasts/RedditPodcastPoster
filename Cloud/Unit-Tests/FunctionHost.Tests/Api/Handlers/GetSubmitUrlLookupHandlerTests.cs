using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Handlers;
using Api.Handlers.SubmitUrl;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Models;
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Handlers;

public class GetSubmitUrlLookupHandlerTests
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
        "When membership lookup finds a unique series, GET submit lookup responds 200 with known true, podcastId, and podcastName.")]
    public async Task known_unique_series_returns_200()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var podcastName = _fixture.CreateTitle();
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        _mocker.GetMock<ISubmitUrlLookupService>()
            .Setup(s => s.LookupAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlLookupResponse
            {
                Known = true,
                Kind = UrlMembershipLookupKinds.PodcastService,
                PodcastId = podcastId,
                PodcastName = podcastName
            });
        var handler = _mocker.CreateInstance<GetSubmitUrlLookupHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            url,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("known").GetBoolean().Should().BeTrue();
        body.GetProperty("podcastId").GetGuid().Should().Be(podcastId);
        body.GetProperty("podcastName").GetString().Should().Be(podcastName);
        body.GetProperty("kind").GetString().Should().Be(UrlMembershipLookupKinds.PodcastService);
    }

    [Fact(DisplayName =
        "When membership lookup is ambiguous, GET submit lookup responds 200 with known false, ambiguous true, and podcastIds, " +
        "because the Add Podcast Series field must still be shown.")]
    public async Task ambiguous_returns_200_with_ids()
    {
        // Arrange
        var first = _fixture.CreateGuid();
        var second = _fixture.CreateGuid();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");
        _mocker.GetMock<ISubmitUrlLookupService>()
            .Setup(s => s.LookupAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlLookupResponse
            {
                Known = false,
                Kind = UrlMembershipLookupKinds.Streaming,
                Ambiguous = true,
                PodcastIds = [first, second]
            });
        var handler = _mocker.CreateInstance<GetSubmitUrlLookupHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            url,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("known").GetBoolean().Should().BeFalse();
        body.GetProperty("ambiguous").GetBoolean().Should().BeTrue();
        body.GetProperty("podcastIds").EnumerateArray().Select(x => x.GetGuid())
            .Should().BeEquivalentTo([first, second]);
    }

    [Fact(DisplayName =
        "When membership lookup finds no stored URL, GET submit lookup responds 200 with known false and the classified kind.")]
    public async Task unknown_streaming_returns_200_with_kind()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");
        _mocker.GetMock<ISubmitUrlLookupService>()
            .Setup(s => s.LookupAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlLookupResponse
            {
                Known = false,
                Kind = UrlMembershipLookupKinds.Streaming
            });
        var handler = _mocker.CreateInstance<GetSubmitUrlLookupHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            url,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("known").GetBoolean().Should().BeFalse();
        body.GetProperty("kind").GetString().Should().Be(UrlMembershipLookupKinds.Streaming);
    }
}
