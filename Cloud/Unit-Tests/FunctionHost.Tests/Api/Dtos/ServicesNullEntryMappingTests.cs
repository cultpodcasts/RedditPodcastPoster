using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1508: a stored <c>"services": { "key": null }</c> entry must not throw when mapping episode DTOs; it is left out.</summary>
public class ServicesNullEntryMappingTests
{
    private static readonly JsonSerializerOptions ApiOptions = new(JsonSerializerDefaults.Web);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public ServicesNullEntryMappingTests()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _, Guid? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
    }

    [Fact(DisplayName =
        "The public episode DTO leaves out a services entry whose stored value is null and keeps the real links, " +
        "so /public/episode/{id} still loads the bookmark instead of returning 500 (#1508).")]
    public void public_episode_drops_null_services_entry()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Services![ServiceKeys.Spotify] = null!;

        // Act
        var dto = episode.ToDto(podcast);
        var services = JsonDocument.Parse(JsonSerializer.Serialize(dto, ApiOptions)).RootElement.GetProperty("services");

        // Assert
        dto.Services.Should().NotContainKey(ServiceKeys.Spotify);
        dto.Services.Should().ContainKey(ServiceKeys.YouTube);
        services.TryGetProperty(ServiceKeys.Spotify, out _).Should().BeFalse();
        services.GetProperty(ServiceKeys.YouTube).GetProperty("url").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName =
        "The curator episode DTO leaves out a services entry whose stored value is null and keeps the real links, " +
        "so the curate episode GET and outgoing list do not fail on that episode (#1508).")]
    public async Task curator_episode_drops_null_services_entry()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Services![ServiceKeys.Spotify] = null!;
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        var dto = await sut.ToDto(episode, podcast, Array.Empty<Subject>());

        // Assert
        dto.Services.Should().NotContainKey(ServiceKeys.Spotify);
        dto.Services.Should().ContainKey(ServiceKeys.YouTube);
    }
}
