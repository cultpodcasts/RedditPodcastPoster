using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// SkipShortEpisodes must follow BypassShortEpisodeChecking: only explicit true includes shorts.
/// </summary>
public class IndexingSkipShortEpisodesRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "INTEGRITY: when indexing a podcast, SkipShortEpisodes is true unless BypassShortEpisodeChecking is explicitly true, because unset/false must not keep short episodes.")]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task index_by_id_derives_skip_short_episodes_from_bypass_flag(
        bool? bypassShortEpisodeChecking,
        bool expectedSkipShortEpisodes)
    {
        // Arrange
        IndexingContext? captured = null;
        var (indexer, podcast) = CreateIndexer(
            bypassShortEpisodeChecking,
            context => captured = context);

        // Act
        await indexer.Index(
            podcast.Id,
            new IndexingContext(ReleasedSince, SkipShortEpisodes: false));

        // Assert
        captured.Should().NotBeNull();
        captured!.SkipShortEpisodes.Should().Be(expectedSkipShortEpisodes);
    }

    [Fact(DisplayName =
        "INTEGRITY: when indexing by podcast name with BypassShortEpisodeChecking unset, SkipShortEpisodes is true, because the API name path must not include shorts without explicit bypass.")]
    public async Task index_by_name_with_unset_bypass_skips_short_episodes()
    {
        // Arrange
        IndexingContext? captured = null;
        var (indexer, podcast) = CreateIndexer(
            bypassShortEpisodeChecking: null,
            context => captured = context);

        // Act
        await indexer.Index(
            podcast.Name,
            new IndexingContext(ReleasedSince, SkipShortEpisodes: false));

        // Assert
        captured.Should().NotBeNull();
        captured!.SkipShortEpisodes.Should().BeTrue();
    }

    private (Indexer Indexer, Podcast Podcast) CreateIndexer(
        bool? bypassShortEpisodeChecking,
        Action<IndexingContext> captureContext)
    {
        var mocker = new AutoMocker();
        var podcastRepository = new InMemoryPodcastRepository();
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        podcast.BypassShortEpisodeChecking = bypassShortEpisodeChecking;
        podcastRepository.Seed(podcast);

        var added = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        added.Id = _fixture.CreateGuid();
        added.PodcastId = podcast.Id;

        mocker.Use<IPodcastRepository>(podcastRepository);
        mocker.Use<IEpisodeRepository>(episodeRepository);
        mocker.Use<IParentIndexingLockStore>(new InMemoryParentIndexingLockStore());
        mocker.Use<TimeProvider>(TimeProvider.System);
        mocker.Use(NullLogger<Indexer>.Instance);

        mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .Callback<Podcast, bool, IndexingContext>((_, _, context) => captureContext(context))
            .ReturnsAsync(new IndexPodcastResult(
                podcast,
                new EpisodeMergeResult([], [added], [], []),
                new FilterResult([]),
                new EnrichmentResults([]),
                SpotifyBypassed: false,
                YouTubeBypassed: false));

        mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(added, It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));

        mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(added, It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));

        mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(
                It.IsAny<Podcast>(),
                It.IsAny<Episode?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));

        return (mocker.CreateInstance<Indexer>(), podcast);
    }
}
