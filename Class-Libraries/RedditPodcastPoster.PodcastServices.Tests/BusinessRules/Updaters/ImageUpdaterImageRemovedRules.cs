using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Updaters;

/// <summary>#1500: a curator-removed image is never refilled by the image updater.</summary>
public class ImageUpdaterImageRemovedRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "The image updater does not refill any image on an episode whose image a curator removed, and makes no API call, " +
        "because only a curator setting an image clears the removal (#1500).")]
    public async Task image_removed_episode_is_not_refilled()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, null);
        episode.ImageRemoved = true;
        var sut = _mocker.CreateInstance<ImageUpdater>();

        // Act
        var updated = await sut.UpdateImages(
            podcast,
            episode,
            new EpisodeImageUpdateRequest(true, true, true, true),
            new IndexingContext());

        // Assert
        updated.Should().BeFalse();
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube).Should().BeNull();
        _mocker.GetMock<IYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never);
    }
}
