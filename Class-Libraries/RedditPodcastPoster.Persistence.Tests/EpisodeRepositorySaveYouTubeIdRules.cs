using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeRepositorySaveYouTubeIdRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Saving an episode with services.youtube.url and no ids.youtube writes the url's video id to ids.youtube, " +
        "because Save is the gate every write path goes through and search reads only the nested id.")]
    public async Task save_fills_missing_youtube_id_from_url()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(_fixture.CreatePodcast(), e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            };
        });
        var written = ArrangeContainer();

        // Act
        await _mocker.CreateInstance<EpisodeRepository>().Save(episode);

        // Assert
        written.Should().ContainSingle().Which.Ids!.YouTube.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "Saving an episode whose ids.youtube is already set keeps it, because the gate only fills a missing id.")]
    public async Task save_keeps_existing_youtube_id()
    {
        // Arrange
        var storedId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(_fixture.CreatePodcast(), e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, storedId);
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId()) }
            };
        });
        var written = ArrangeContainer();

        // Act
        await _mocker.CreateInstance<EpisodeRepository>().Save(episode);

        // Assert
        written.Should().ContainSingle().Which.Ids!.YouTube.Should().Be(storedId);
    }

    private List<Episode> ArrangeContainer()
    {
        var written = new List<Episode>();
        var container = _mocker.GetMock<Container>();
        container
            .Setup(c => c.ReadItemAsync<Episode>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("synthetic", HttpStatusCode.NotFound, 0, string.Empty, 0));
        container
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<Episode>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<Episode, PartitionKey?, ItemRequestOptions, CancellationToken>((e, _, _, _) => written.Add(e))
            .ReturnsAsync(Mock.Of<ItemResponse<Episode>>());
        return written;
    }
}
