using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Providers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class RemovedPodcastIndexingRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a podcast has Removed=true and EpisodeIncludeTitleRegex set, PodcastsUpdater does not attempt to index it.")]
    public async Task removed_podcast_with_episode_include_regex_is_not_indexed()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Removed = true;
        podcast.EpisodeIncludeTitleRegex = @"\b(?:cult|cults)\b";
        podcast.IndexAllEpisodes = false;

        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast);

        var mocker = new AutoMocker();
        mocker.Use<IPodcastRepository>(repository);
        mocker.Use(NullLogger<PodcastsUpdater>.Instance);
        mocker.Use(Mock.Of<IIndexablePodcastIdProvider>());
        var sut = mocker.CreateInstance<PodcastsUpdater>();

        // Act
        await sut.UpdatePodcasts([podcast.Id], new IndexingContext(ReleasedSince));

        // Assert
        mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), It.IsAny<bool>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When a podcast has Removed=true and IndexAllEpisodes=true, PodcastsUpdater does not attempt to index it.")]
    public async Task removed_podcast_with_index_all_episodes_is_not_indexed()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Removed = true;
        podcast.IndexAllEpisodes = true;

        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast);

        var mocker = new AutoMocker();
        mocker.Use<IPodcastRepository>(repository);
        mocker.Use(NullLogger<PodcastsUpdater>.Instance);
        mocker.Use(Mock.Of<IIndexablePodcastIdProvider>());
        var sut = mocker.CreateInstance<PodcastsUpdater>();

        // Act
        await sut.UpdatePodcasts([podcast.Id], new IndexingContext(ReleasedSince));

        // Assert
        mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), It.IsAny<bool>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }
}
