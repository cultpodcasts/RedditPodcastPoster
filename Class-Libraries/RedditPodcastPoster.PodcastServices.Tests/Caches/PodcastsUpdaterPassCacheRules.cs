using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Caches;

public class PodcastsUpdaterPassCacheRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _repository = new();

    public PodcastsUpdaterPassCacheRules()
    {
        _mocker.Use<IPodcastRepository>(_repository);
        _mocker.Use<ILogger<PodcastsUpdater>>(NullLogger<PodcastsUpdater>.Instance);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(new PodcastUpdateBudgetOptions()));
        _mocker.Use<TimeProvider>(TimeProvider.System);
    }

    [Fact(DisplayName =
        "UpdatePodcasts clears the pass API cache once per auto-indexed podcast.")]
    public async Task UpdatePodcasts_clears_pass_cache_once_per_auto_indexed_podcast()
    {
        // Arrange
        var podcast1 = AutoIndexPodcast();
        var podcast2 = AutoIndexPodcast();
        var skipped = AutoIndexPodcast();
        skipped.IndexAllEpisodes = false;
        skipped.EpisodeIncludeTitleRegex = string.Empty;
        _repository.Seed(podcast1);
        _repository.Seed(podcast2);
        _repository.Seed(skipped);
        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync((Podcast podcast, bool _, IndexingContext _) => SuccessResult(podcast));
        var updater = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        await updater.UpdatePodcasts(
            [podcast1.Id, podcast2.Id, skipped.Id],
            new IndexingContext(ReleasedSince));

        // Assert
        _mocker.GetMock<IPodcastPassApiCache>().Verify(x => x.Clear(), Times.Exactly(2));
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
    }

    [Fact(DisplayName =
        "UpdatePodcasts clears the pass API cache when podcast update throws.")]
    public async Task UpdatePodcasts_clears_pass_cache_when_update_throws()
    {
        // Arrange
        var podcast = AutoIndexPodcast();
        _repository.Seed(podcast);
        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));
        var updater = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var success = await updater.UpdatePodcasts(
            [podcast.Id],
            new IndexingContext(ReleasedSince));

        // Assert
        success.Should().BeFalse();
        _mocker.GetMock<IPodcastPassApiCache>().Verify(x => x.Clear(), Times.Once);
    }

    private Podcast AutoIndexPodcast()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        return podcast;
    }

    private static IndexPodcastResult SuccessResult(Podcast podcast) =>
        new(
            podcast,
            EpisodeMergeResult.Empty,
            new FilterResult([]),
            new EnrichmentResults([]),
            SpotifyBypassed: false,
            YouTubeBypassed: false);
}
