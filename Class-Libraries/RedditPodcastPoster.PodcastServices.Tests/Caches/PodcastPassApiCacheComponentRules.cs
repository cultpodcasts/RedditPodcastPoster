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
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Caches;

/// <summary>
/// Assembles real pass-cache + CachedApplePodcastService; mocks platform HTTP and PodcastUpdater edges.
/// </summary>
public class PodcastPassApiCacheComponentRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly ApplePodcastId _appleId;
    private readonly CachedApplePodcastService _cachedApple;

    public PodcastPassApiCacheComponentRules()
    {
        _appleId = new ApplePodcastId(_fixture.CreateAppleId());
        _mocker.Use<ILogger<CachedApplePodcastService>>(NullLogger<CachedApplePodcastService>.Instance);
        _mocker.Use<ILogger<PodcastPassApiCache>>(NullLogger<PodcastPassApiCache>.Instance);
        _mocker.Use<ILogger<PodcastsUpdater>>(NullLogger<PodcastsUpdater>.Instance);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(new PodcastUpdateBudgetOptions()));
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.GetMock<IApplePodcastService>()
            .Setup(x => x.GetEpisodes(_appleId, It.IsAny<IndexingContext>()))
            .ReturnsAsync(() => [CreateAppleEpisode()]);
        _cachedApple = _mocker.CreateInstance<CachedApplePodcastService>();
        _mocker.Use<IEnumerable<IPodcastPassApiCacheSource>>([_cachedApple]);
        _mocker.Use<IPodcastPassApiCache>(_mocker.CreateInstance<PodcastPassApiCache>());
    }

    [Fact(DisplayName =
        "CachedApplePodcastService returns a cached episode list on a second fetch with the same key.")]
    public async Task CachedApple_resolves_already_fetched_episodes_without_rehit()
    {
        // Arrange
        var indexingContext = new IndexingContext(ReleasedSince);

        // Act
        var first = await _cachedApple.GetEpisodes(_appleId, indexingContext);
        var second = await _cachedApple.GetEpisodes(_appleId, indexingContext);

        // Assert
        first.Should().BeEquivalentTo(second);
        _mocker.GetMock<IApplePodcastService>().Verify(x => x.GetEpisodes(_appleId, indexingContext), Times.Once);
    }

    [Fact(DisplayName =
        "PodcastPassApiCache.Clear forces CachedApplePodcastService to refetch after a hit.")]
    public async Task Clear_forces_CachedApple_to_refetch()
    {
        // Arrange
        var indexingContext = new IndexingContext(ReleasedSince);
        await _cachedApple.GetEpisodes(_appleId, indexingContext);
        await _cachedApple.GetEpisodes(_appleId, indexingContext);

        // Act
        _mocker.Get<IPodcastPassApiCache>().Clear();
        await _cachedApple.GetEpisodes(_appleId, indexingContext);

        // Assert
        _mocker.GetMock<IApplePodcastService>()
            .Verify(x => x.GetEpisodes(_appleId, indexingContext), Times.Exactly(2));
    }

    [Fact(DisplayName =
        "PodcastsUpdater clears pass caches between podcasts so within-podcast hits do not leak across podcasts.")]
    public async Task UpdatePodcasts_clears_between_podcasts_while_preserving_within_podcast_hits()
    {
        // Arrange
        var podcast1 = AutoIndexPodcast();
        var podcast2 = AutoIndexPodcast();
        var repository = new InMemoryPodcastRepository();
        repository.Seed(podcast1);
        repository.Seed(podcast2);
        _mocker.Use<IPodcastRepository>(repository);
        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .Returns(async (Podcast podcast, bool _, IndexingContext podcastCtx) =>
            {
                // Simulate discovery + enrichment both needing Apple episodes (within-podcast hit).
                await _cachedApple.GetEpisodes(_appleId, podcastCtx);
                await _cachedApple.GetEpisodes(_appleId, podcastCtx);
                return SuccessResult(podcast);
            });
        var updater = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        await updater.UpdatePodcasts([podcast1.Id, podcast2.Id], new IndexingContext(ReleasedSince));

        // Assert
        // Two podcasts × one underlying fetch each (second call per podcast is a cache hit).
        _mocker.GetMock<IApplePodcastService>().Verify(
            x => x.GetEpisodes(_appleId, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()),
            Times.Exactly(2));
    }

    private Podcast AutoIndexPodcast()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        return podcast;
    }

    private AppleEpisode CreateAppleEpisode()
    {
        var id = _fixture.CreateAppleId();
        return new AppleEpisode(
            id,
            Title: _fixture.CreateTitle(),
            Release: ReleasedSince.AddDays(1),
            Duration: _fixture.CreateDuration(),
            Url: new Uri($"https://podcasts.apple.com/us/podcast/episode/id{id}"),
            Description: _fixture.CreateTitle(),
            Explicit: false);
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
