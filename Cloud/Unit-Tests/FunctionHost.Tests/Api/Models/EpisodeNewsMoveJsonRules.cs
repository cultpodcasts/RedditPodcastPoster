using System.Text.Json;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using FluentAssertions;
using Api.Dtos.Extensions;
using Api.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class EpisodeNewsMoveJsonRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "The move-to-news body binds newsOrganisationId, newsOrganisationName, createOrganisation and publisher onto the command request.")]
    public void request_fields_bind()
    {
        // Arrange
        var newsOrganisationId = _fixture.CreateGuid();
        var newsOrganisationName = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var json = JsonSerializer.Serialize(new
        {
            newsOrganisationId, newsOrganisationName, createOrganisation = true, publisher
        });

        // Act
        var request = JsonSerializer.Deserialize<global::Api.Dtos.EpisodeNewsMoveRequest>(json).ToModel();

        // Assert
        request.NewsOrganisationId.Should().Be(newsOrganisationId);
        request.NewsOrganisationName.Should().Be(newsOrganisationName);
        request.CreateOrganisation.Should().BeTrue();
        request.Publisher.Should().Be(publisher);
    }

    [Fact(DisplayName =
        "The batch move-to-news body binds episodeIds and the organisation fields onto a command for the route's podcast.")]
    public void batch_request_binds_episode_ids()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeIds = new[] { _fixture.CreateGuid(), _fixture.CreateGuid() };
        var newsOrganisationName = _fixture.CreateTitle();
        var json = JsonSerializer.Serialize(new { episodeIds, newsOrganisationName, createOrganisation = true });

        // Act
        var command = JsonSerializer.Deserialize<global::Api.Dtos.PodcastNewsMoveRequest>(json).ToCommand(podcastId);

        // Assert
        command.PodcastId.Should().Be(podcastId);
        command.EpisodeIds.Should().Equal(episodeIds);
        command.Request.NewsOrganisationName.Should().Be(newsOrganisationName);
        command.Request.CreateOrganisation.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A missing batch move-to-news body maps to no episode ids and no target, which the service rejects as a bad request.")]
    public void missing_batch_body_is_empty_command()
    {
        // Arrange
        global::Api.Dtos.PodcastNewsMoveRequest? body = null;

        // Act
        var command = body.ToCommand(_fixture.CreateGuid());

        // Assert
        command.EpisodeIds.Should().BeEmpty();
        command.Request.NewsOrganisationId.Should().BeNull();
        command.Request.NewsOrganisationName.Should().BeNull();
        command.Request.CreateOrganisation.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The move-to-news response carries newsOrganisationId, newsOrganisationName, createdOrganisation, the per-id lists and failureIndexing, and omits a null error and episodeId.")]
    public void response_wire_names()
    {
        // Arrange
        var moved = _fixture.CreateGuid();
        var result = new EpisodeNewsMoveResult(
            EpisodeNewsMoveStatus.Accepted,
            _fixture.CreateGuid(),
            _fixture.CreateTitle(),
            CreatedOrganisation: true,
            MovedEpisodeIds: [moved]);

        // Act
        var json = JsonSerializer.Serialize(result.ToDto());

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("newsOrganisationId").GetGuid().Should().Be(result.NewsOrganisationId!.Value);
        root.GetProperty("newsOrganisationName").GetString().Should().Be(result.NewsOrganisationName);
        root.GetProperty("createdOrganisation").GetBoolean().Should().BeTrue();
        root.GetProperty("movedEpisodeIds")[0].GetGuid().Should().Be(moved);
        root.GetProperty("alreadyMovedEpisodeIds").GetArrayLength().Should().Be(0);
        root.GetProperty("notFoundEpisodeIds").GetArrayLength().Should().Be(0);
        root.GetProperty("conflictEpisodeIds").GetArrayLength().Should().Be(0);
        root.GetProperty("failureIndexing").GetBoolean().Should().BeFalse();
        root.TryGetProperty("error", out _).Should().BeFalse();
        root.TryGetProperty("episodeId", out _).Should().BeFalse();
    }
}
