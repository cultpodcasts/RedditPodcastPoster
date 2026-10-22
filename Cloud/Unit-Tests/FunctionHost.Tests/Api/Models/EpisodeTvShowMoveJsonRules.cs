using System.Text.Json;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using FluentAssertions;
using Api.Dtos.Extensions;
using Api.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class EpisodeTvShowMoveJsonRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "The move-to-TV-show body binds tvShowId, tvShowName, createShow, publisher and title onto the command request.")]
    public void request_fields_bind()
    {
        // Arrange
        var tvShowId = _fixture.CreateGuid();
        var tvShowName = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var title = _fixture.CreateTitle();
        var json = JsonSerializer.Serialize(new { tvShowId, tvShowName, createShow = true, publisher, title });

        // Act
        var request = JsonSerializer.Deserialize<global::Api.Dtos.EpisodeTvShowMoveRequest>(json).ToModel();

        // Assert
        request.TvShowId.Should().Be(tvShowId);
        request.TvShowName.Should().Be(tvShowName);
        request.CreateShow.Should().BeTrue();
        request.Publisher.Should().Be(publisher);
        request.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "A missing move-to-TV-show body maps to an empty request (no target, createShow false), which the service rejects as a bad request.")]
    public void missing_body_is_empty_request()
    {
        // Arrange
        global::Api.Dtos.EpisodeTvShowMoveRequest? body = null;

        // Act
        var request = body.ToModel();

        // Assert
        request.TvShowId.Should().BeNull();
        request.TvShowName.Should().BeNull();
        request.CreateShow.Should().BeFalse();
        request.Publisher.Should().BeNull();
        request.Title.Should().BeNull();
    }

    [Fact(DisplayName =
        "The move-to-TV-show response carries tvShowId, tvShowName, episodeId, createdShow and failureIndexing, and omits a null error, as the website reads them.")]
    public void response_wire_names()
    {
        // Arrange
        var result = new EpisodeTvShowMoveResult(
            EpisodeTvShowMoveStatus.Accepted,
            _fixture.CreateGuid(),
            _fixture.CreateTitle(),
            _fixture.CreateGuid(),
            CreatedShow: true);

        // Act
        var json = JsonSerializer.Serialize(result.ToDto());

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("tvShowId").GetGuid().Should().Be(result.TvShowId!.Value);
        document.RootElement.GetProperty("tvShowName").GetString().Should().Be(result.TvShowName);
        document.RootElement.GetProperty("episodeId").GetGuid().Should().Be(result.EpisodeId!.Value);
        document.RootElement.GetProperty("createdShow").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("failureIndexing").GetBoolean().Should().BeFalse();
        document.RootElement.TryGetProperty("error", out _).Should().BeFalse();
    }
}
