using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class PostEpisodeHandlerTests
{
    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact(DisplayName =
        "Plain English rule: when episode update is accepted, then respond 202 with tweetDeleted, blueskyPostDeleted, and searchIndexerState in the JSON body, because curators need social and indexer outcomes.")]
    public async Task accepted_returns_202_with_outcome_json()
    {
        // Arrange
        var outcome = new EpisodeUpdateOutcome
        {
            TweetDeleted = true,
            BlueskyPostDeleted = false,
            SearchIndexer = new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed }
        };
        var service = new Mock<IEpisodeUpdateService>();
        service.Setup(s => s.UpdateAsync(It.IsAny<EpisodeChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeUpdateResult(EpisodeUpdateStatus.Accepted, outcome));

        var handler = new PostEpisodeHandler(service.Object, NullLogger<PostEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var wrapper = new EpisodeChangeRequestWrapper(Guid.NewGuid(), Guid.NewGuid(), new EpisodeChangeRequest());

        // Act
        var result = await handler.Handle(new HandlerContext(req.Object, null), wrapper, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("tweetDeleted").GetBoolean().Should().BeTrue();
        body.GetProperty("blueskyPostDeleted").GetBoolean().Should().BeFalse();
        body.GetProperty("searchIndexerState").GetString().Should().Be("Executed");
    }

    [Fact(DisplayName =
        "Plain English rule: when episode update target is not found, then respond 404, because the handler must not pretend an update succeeded.")]
    public async Task not_found_returns_404()
    {
        // Arrange
        var service = new Mock<IEpisodeUpdateService>();
        service.Setup(s => s.UpdateAsync(It.IsAny<EpisodeChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound));

        var handler = new PostEpisodeHandler(service.Object, NullLogger<PostEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new EpisodeChangeRequestWrapper(Guid.NewGuid(), Guid.NewGuid(), new EpisodeChangeRequest()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when episode update fails, then respond 500 with an error property, because clients need a failure signal.")]
    public async Task failed_returns_500_with_error_body()
    {
        // Arrange
        var service = new Mock<IEpisodeUpdateService>();
        service.Setup(s => s.UpdateAsync(It.IsAny<EpisodeChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeUpdateResult(EpisodeUpdateStatus.Failed));

        var handler = new PostEpisodeHandler(service.Object, NullLogger<PostEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new EpisodeChangeRequestWrapper(Guid.NewGuid(), Guid.NewGuid(), new EpisodeChangeRequest()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
