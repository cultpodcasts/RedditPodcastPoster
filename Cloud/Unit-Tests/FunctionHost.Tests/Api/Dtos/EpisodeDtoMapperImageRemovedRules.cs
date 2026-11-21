using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Dtos.Mapping;
using Api.Serialization;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1500 / #1502: the curator episode DTO carries the removed-image flag, so the generated contract has it.</summary>
public class EpisodeDtoMapperImageRemovedRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public EpisodeDtoMapperImageRemovedRules()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
    }

    [Fact(DisplayName =
        "Curator episode DTO: when a curator removed the episode's image, imageRemoved is true on the DTO and in the HTTP JSON (#1500, #1502).")]
    public async Task removed_image_sets_the_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, null);
        episode.ImageRemoved = true;
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        var dto = await sut.ToDto(episode, podcast, Array.Empty<Subject>());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiJson.CreateHttpSerializerOptions()));

        // Assert
        dto.ImageRemoved.Should().BeTrue();
        json.RootElement.GetProperty("imageRemoved").GetBoolean().Should().BeTrue();
    }

    [Theory(DisplayName =
        "Curator episode DTO: when the image was not removed (flag null or false), imageRemoved is null and the key is omitted from the HTTP JSON (#1500, #1502).")]
    [InlineData(null)]
    [InlineData(false)]
    public async Task not_removed_omits_the_flag(bool? stored)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.ImageRemoved = stored;
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        var dto = await sut.ToDto(episode, podcast, Array.Empty<Subject>());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiJson.CreateHttpSerializerOptions()));

        // Assert
        dto.ImageRemoved.Should().BeNull();
        json.RootElement.TryGetProperty("imageRemoved", out _).Should().BeFalse();
    }
}
