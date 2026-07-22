using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Providers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Caches;

/// <summary>
/// Assembles real pass-cache + CachedApplePodcastService; mocks platform HTTP and PodcastUpdater edges.
/// </summary>
public class PodcastPassApiCacheComponentRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "CachedApplePodcastService returns a cached episode list on a second fetch with the same key.")]
    public async Task CachedApple_resolves_already_fetched_episodes_without_rehit()
    {
        var appleId = new ApplePodcastId(42);
        var indexingContext = new IndexingContext(ReleasedSince);
        var underlying = new Mock<IApplePodcastService>();
        underlying
            .Setup(x => x.GetEpisodes(appleId, indexingContext))
            .ReturnsAsync([CreateAppleEpisode(1)]);

        var cached = new CachedApplePodcastService(
            underlying.Object,
            NullLogger<CachedApplePodcastService>.Instance);

        var first = await cached.GetEpisodes(appleId, indexingContext);
        var second = await cached.GetEpisodes(appleId, indexingContext);

        first.Should().BeEquivalentTo(second);
        underlying.Verify(x => x.GetEpisodes(appleId, indexingContext), Times.Once);
    }

    [Fact(DisplayName =
        "PodcastPassApiCache.Clear forces CachedApplePodcastService to refetch after a hit.")]
    public async Task Clear_forces_CachedApple_to_refetch()
    {
        var appleId = new ApplePodcastId(42);
        var indexingContext = new IndexingContext(ReleasedSince);
        var underlying = new Mock<IApplePodcastService>();
        underlying
            .Setup(x => x.GetEpisodes(appleId, indexingContext))
            .ReturnsAsync([CreateAppleEpisode(1)]);

        var cached = new CachedApplePodcastService(
            underlying.Object,
            NullLogger<CachedApplePodcastService>.Instance);
        var passCache = new PodcastPassApiCache(
            [cached],
            NullLogger<PodcastPassApiCache>.Instance);

        await cached.GetEpisodes(appleId, indexingContext);
        await cached.GetEpisodes(appleId, indexingContext);
        underlying.Verify(x => x.GetEpisodes(appleId, indexingContext), Times.Once);

        passCache.Clear();

        await cached.GetEpisodes(appleId, indexingContext);
        underlying.Verify(x => x.GetEpisodes(appleId, indexingContext), Times.Exactly(2));
    }

    [Fact(DisplayName =
        "PodcastsUpdater clears pass caches between podcasts so within-podcast hits do not leak across podcasts.")]
    public async Task UpdatePodcasts_clears_between_podcasts_while_preserving_within_podcast_hits()
    {
        var podcast1 = AutoIndexPodcast();
        var podcast2 = AutoIndexPodcast();
        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast1);
        repository.Seed(podcast2);

        var appleId = new ApplePodcastId(99);
        var indexingContext = new IndexingContext(ReleasedSince);
        var underlying = new Mock<IApplePodcastService>();
        underlying
            .Setup(x => x.GetEpisodes(appleId, It.IsAny<IndexingContext>()))
            .ReturnsAsync([CreateAppleEpisode(7)]);

        var cachedApple = new CachedApplePodcastService(
            underlying.Object,
            NullLogger<CachedApplePodcastService>.Instance);
        var passCache = new PodcastPassApiCache(
            [cachedApple],
            NullLogger<PodcastPassApiCache>.Instance);

        var podcastUpdater = new Mock<IPodcastUpdater>();
        podcastUpdater
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .Returns(async (Podcast podcast, bool _, IndexingContext podcastCtx) =>
            {
                // Simulate discovery + enrichment both needing Apple episodes (within-podcast hit).
                await cachedApple.GetEpisodes(appleId, podcastCtx);
                await cachedApple.GetEpisodes(appleId, podcastCtx);
                return SuccessResult(podcast);
            });

        var updater = new PodcastsUpdater(
            Mock.Of<IIndexablePodcastIdProvider>(),
            podcastUpdater.Object,
            repository,
            passCache,
            NullLogger<PodcastsUpdater>.Instance);

        await updater.UpdatePodcasts([podcast1.Id, podcast2.Id], indexingContext);

        // Two podcasts × one underlying fetch each (second call per podcast is a cache hit).
        underlying.Verify(
            x => x.GetEpisodes(appleId, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
        podcastUpdater.Verify(
            x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
    }

    private Podcast AutoIndexPodcast()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        return podcast;
    }

    private static AppleEpisode CreateAppleEpisode(long id) =>
        new(
            id,
            Title: $"Episode {id}",
            Release: ReleasedSince.AddDays(1),
            Duration: TimeSpan.FromMinutes(30),
            Url: new Uri($"https://example.com/apple/{id}"),
            Description: "desc",
            Explicit: false);

    private static IndexPodcastResult SuccessResult(Podcast podcast) =>
        new(
            podcast,
            EpisodeMergeResult.Empty,
            new FilterResult([]),
            new EnrichmentResults([]),
            SpotifyBypassed: false,
            YouTubeBypassed: false);
}
