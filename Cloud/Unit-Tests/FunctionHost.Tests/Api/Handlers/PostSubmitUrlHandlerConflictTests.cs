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
using Xunit;
using FunctionHost.Tests.Api;

namespace FunctionHost.Tests.Api.Handlers;

public class PostSubmitUrlHandlerConflictTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private SubmitUrlResult _serviceResult = new(SubmitUrlStatus.Failed);

    public PostSubmitUrlHandlerConflictTests()
    {
        _mocker.GetMock<ISubmitUrlService>()
            .Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _serviceResult);
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps Conflict to 409 with the ambiguous podcast id list.")]
    public async Task conflict_returns_409_uuid_array()
    {
        // Arrange
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.Conflict, AmbiguousPodcasts: [firstId, secondId]);
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var ids = doc.RootElement.EnumerateArray().Select(e => e.GetGuid()).ToArray();
        ids.Should().BeEquivalentTo([firstId, secondId]);
    }

    [Fact(DisplayName =
        "When the submit service reports a missing podcast, the handler returns HTTP 404.")]
    public async Task podcast_not_found_returns_404()
    {
        // Arrange
        _serviceResult = new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: _fixture.Create<string>());
        var handler = _mocker.CreateInstance<PostSubmitUrlHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri($"https://example.com/{_fixture.Create<string>()}") },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
