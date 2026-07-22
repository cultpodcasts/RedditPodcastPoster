using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Providers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Caches;

public class PodcastsUpdaterPassCacheRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "UpdatePodcasts clears the pass API cache once per auto-indexed podcast.")]
    public async Task UpdatePodcasts_clears_pass_cache_once_per_auto_indexed_podcast()
    {
        var podcast1 = AutoIndexPodcast();
        var podcast2 = AutoIndexPodcast();
        var skipped = AutoIndexPodcast();
        skipped.IndexAllEpisodes = false;
        skipped.EpisodeIncludeTitleRegex = string.Empty;

        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast1);
        repository.Seed(podcast2);
        repository.Seed(skipped);

        var podcastUpdater = new Mock<IPodcastUpdater>();
        podcastUpdater
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync((Podcast podcast, bool _, IndexingContext _) => SuccessResult(podcast));

        var passApiCache = new Mock<IPodcastPassApiCache>();
        var updater = CreateUpdater(repository, podcastUpdater.Object, passApiCache.Object);

        await updater.UpdatePodcasts(
            [podcast1.Id, podcast2.Id, skipped.Id],
            new IndexingContext(ReleasedSince));

        passApiCache.Verify(x => x.Clear(), Times.Exactly(2));
        podcastUpdater.Verify(
            x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
    }

    [Fact(DisplayName =
        "UpdatePodcasts clears the pass API cache when podcast update throws.")]
    public async Task UpdatePodcasts_clears_pass_cache_when_update_throws()
    {
        var podcast = AutoIndexPodcast();
        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast);

        var podcastUpdater = new Mock<IPodcastUpdater>();
        podcastUpdater
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException("update failed"));

        var passApiCache = new Mock<IPodcastPassApiCache>();
        var updater = CreateUpdater(repository, podcastUpdater.Object, passApiCache.Object);

        var success = await updater.UpdatePodcasts(
            [podcast.Id],
            new IndexingContext(ReleasedSince));

        success.Should().BeFalse();
        passApiCache.Verify(x => x.Clear(), Times.Once);
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

    private static PodcastsUpdater CreateUpdater(
        InMemoryPodcastRepository repository,
        IPodcastUpdater podcastUpdater,
        IPodcastPassApiCache passApiCache) =>
        new(
            Mock.Of<IIndexablePodcastIdProvider>(),
            podcastUpdater,
            repository,
            passApiCache,
            NullLogger<PodcastsUpdater>.Instance);
}
