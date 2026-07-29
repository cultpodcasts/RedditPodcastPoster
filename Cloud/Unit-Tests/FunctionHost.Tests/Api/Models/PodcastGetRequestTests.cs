using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastGetRequestTests
{
    [Fact(DisplayName =
        "Route identifier that is a podcast guid: request resolves by PodcastId, because curator UIs pass ids from episode review.")]
    public void guid_identifier_resolves_by_podcast_id()
    {
        // Arrange
        var podcastId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();

        // Act
        var request = PodcastGetRequest.FromRouteIdentifier(podcastId.ToString("D"), episodeId);

        // Assert
        request.PodcastId.Should().Be(podcastId);
        request.PodcastName.Should().BeNull();
        request.EpisodeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Route identifier that is a podcast name with episode id: request keeps name + episode id for disambiguation.")]
    public void name_identifier_keeps_name_and_episode_id()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";
        var episodeId = Guid.NewGuid();

        // Act
        var request = PodcastGetRequest.FromRouteIdentifier(podcastName, episodeId);

        // Assert
        request.PodcastId.Should().BeNull();
        request.PodcastName.Should().Be(podcastName);
        request.EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "Route identifier that is a podcast name without episode id: request looks up by name alone.")]
    public void name_identifier_without_episode_id_looks_up_by_name()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";

        // Act
        var request = PodcastGetRequest.FromRouteIdentifier(podcastName);

        // Assert
        request.PodcastId.Should().BeNull();
        request.PodcastName.Should().Be(podcastName);
        request.EpisodeId.Should().BeNull();
    }
}
