using System.Text.Json;
using Api.Dtos.Extensions;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1500: the public episode DTO exposes a curator-removed image as no image plus <c>imageRemoved</c>. #1502: its ids and services stay JSON-identical to the domain types.</summary>
public class PublicEpisodeImageRemovedTests
{
    private static readonly JsonSerializerOptions ApiOptions = new(JsonSerializerDefaults.Web);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "The public episode DTO has no image and imageRemoved=true when a curator removed the image, so the site shows no art instead of the YouTube thumbnail (#1500).")]
    public void removed_image_maps_to_no_image_and_the_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, null);
        episode.ImageRemoved = true;

        // Act
        var dto = episode.ToDto(podcast);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiOptions)).RootElement;

        // Assert
        dto.Image.Should().BeNull();
        dto.ImageRemoved.Should().BeTrue();
        json.GetProperty("imageRemoved").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName =
        "The public episode DTO omits imageRemoved for an episode whose image was not removed (#1500).")]
    public void kept_image_omits_the_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var dto = episode.ToDto(podcast);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiOptions)).RootElement;

        // Assert
        dto.ImageRemoved.Should().BeNull();
        json.TryGetProperty("imageRemoved", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "The public episode DTO writes ids and services with the same JSON as the domain EpisodeIds and ServiceLink, so exporting it to the contracts package does not change the public wire bytes (#1502).")]
    public void ids_and_services_json_matches_the_domain()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var dto = episode.ToDto(podcast);
        var dtoIds = JsonSerializer.Serialize(dto.Ids, ApiOptions);
        var dtoServices = JsonSerializer.Serialize(dto.Services, ApiOptions);
        var domainIds = JsonSerializer.Serialize(episode.Ids, ApiOptions);
        var domainServices = JsonSerializer.Serialize(episode.Services, ApiOptions);

        // Assert
        dtoIds.Should().Be(domainIds);
        dtoServices.Should().Be(domainServices);
        dto.Services.Should().NotBeNullOrEmpty();
    }
}
