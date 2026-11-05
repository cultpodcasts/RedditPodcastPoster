using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Persistence.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Concurrent on-demand and hourly indexes of the same parent must not overlap (#1160): the second run is Busy /
/// skipped while the first holds the LookUps lease.
/// </summary>
public class ParentIndexingLockRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(7);
    private static readonly TimeSpan PostableDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration + TimeSpan.FromMinutes(5);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryParentIndexingLockStore _locks = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly RecordingLogger<PodcastsUpdater> _podcastsLogger = new();
    private readonly RecordingLogger<CatalogueParentsUpdater> _catalogueLogger = new();
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly List<Guid> _podcastsUpdated = [];
    private IList<Episode> _discovered = [];

    public ParentIndexingLockRules()
    {
        _mocker.Use<IParentIndexingLockStore>(_locks);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use<ILogger<PodcastsUpdater>>(_podcastsLogger);
        _mocker.Use<ILogger<CatalogueParentsUpdater>>(_catalogueLogger);
        _mocker.Use(Options.Create(new PodcastUpdateBudgetOptions()));
        _mocker.Use(Options.Create(new PodcastIndexingStageLogOptions()));
        _mocker.Use(new PodcastUpdateBudgetCutTracker());
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use(NullLogger<Indexer>.Instance);

        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync((Podcast podcast, bool _, IndexingContext _) =>
            {
                _podcastsUpdated.Add(podcast.Id);
                return new IndexPodcastResult(
                    podcast,
                    new EpisodeMergeResult([], [], [], []),
                    new FilterResult([]),
                    new EnrichmentResults([]),
                    SpotifyBypassed: false,
                    YouTubeBypassed: false);
            });

        var tvShows = _mocker.GetMock<ITvShowRepository>();
        tvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
        tvShows.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<TvShow>());
        tvShows.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(true);

        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<TvShowEpisode>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Returns(Task.CompletedTask);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(true);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<NewsReport>());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() => [.. _discovered]);
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> existing, IEnumerable<Episode> added) =>
                new EpisodeMergeResult(existing.ToList(), added.ToList(), [], []));
        _mocker.GetMock<IPodcastFilter>()
            .Setup(x => x.Filter(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<List<string>>()))
            .Returns(new FilterResult([]));
        _mocker.GetMock<IEliminationTermsProvider>()
            .Setup(x => x.GetEliminationTerms())
            .Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync())
            .ReturnsAsync(() => _mocker.GetMock<IEliminationTermsProvider>().Object);

        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));
        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<People.Models.GuestEnrichmentOptions?>()))
            .ReturnsAsync(new People.Models.EnrichGuestsResult([], []));
        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(
                It.IsAny<Podcast>(),
                It.IsAny<Episode?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));
        _mocker.Use<IEpisodeRepository>(new InMemoryEpisodeRepository());
    }

    [Fact(DisplayName =
        "Hourly podcast index: when another index already holds the parent's lease, that podcast is skipped with the " +
        "stable AlreadyRunning log and the next podcast is still indexed, because concurrent runs must not duplicate " +
        "episodes or clobber lastIndexed.")]
    public async Task hourly_podcast_skips_when_lease_held()
    {
        // Arrange
        var held = AutoIndexPodcast();
        var free = AutoIndexPodcast();
        _podcasts.Seed(held);
        _podcasts.Seed(free);
        _locks.SeedMarker(held.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        var sut = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var success = await sut.UpdatePodcasts([held.Id, free.Id], new IndexingContext(ReleasedSince));

        // Assert
        success.Should().BeTrue();
        _podcastsUpdated.Should().Equal(free.Id);
        _podcastsLogger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(PodcastsUpdater.AlreadyRunningMessagePrefix) &&
            entry.Message.Contains($"podcast-id='{held.Id}'"));
        _locks.IsHeld(held.Id).Should().BeTrue();
        _locks.IsHeld(free.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "On-demand podcast index: when another index already holds the lease, Indexer returns AlreadyRunning and does " +
        "not call the updater, because the API must answer 409 instead of duplicating the pass.")]
    public async Task on_demand_podcast_is_already_running_when_lease_held()
    {
        // Arrange
        var podcast = AutoIndexPodcast();
        _podcasts.Seed(podcast);
        _locks.SeedMarker(podcast.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        var indexer = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await indexer.Index(podcast.Id, new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.AlreadyRunning);
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), It.IsAny<bool>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "On-demand catalogue parent index: when another index already holds the lease, the result is AlreadyRunning " +
        "and discovery never runs, because the API must answer 409 for TV/news the same way as for podcasts.")]
    public async Task on_demand_catalogue_parent_is_already_running_when_lease_held()
    {
        // Arrange
        var organisation = SeedOrganisation();
        _locks.SeedMarker(organisation.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var result = await sut.IndexParent(organisation, new IndexingContext(ReleasedSince));

        // Assert
        result.Status.Should().Be(CatalogueParentIndexStatus.AlreadyRunning);
        result.Changed.Should().BeEmpty();
        _mocker.GetMock<IEpisodeProvider>().Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()),
            Times.Never);
        _catalogueLogger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.AlreadyRunningMessagePrefix) &&
            entry.Message.Contains($"parent-id='{organisation.Id}'"));
    }

    [Fact(DisplayName =
        "On-demand TV show index: when another index already holds the lease, the result is AlreadyRunning and " +
        "discovery never runs, because each catalogue entry point must refuse Busy the same way (#1160).")]
    public async Task on_demand_tv_show_is_already_running_when_lease_held()
    {
        // Arrange
        var show = SeedTvShow();
        _locks.SeedMarker(show.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var result = await sut.IndexParent(show, new IndexingContext(ReleasedSince));

        // Assert
        result.Status.Should().Be(CatalogueParentIndexStatus.AlreadyRunning);
        result.Changed.Should().BeEmpty();
        _mocker.GetMock<IEpisodeProvider>().Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()),
            Times.Never);
        _catalogueLogger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.AlreadyRunningMessagePrefix) &&
            entry.Message.Contains($"parent-id='{show.Id}'") &&
            entry.Message.Contains("kind='TvShow'"));
    }

    [Fact(DisplayName =
        "Hourly catalogue parent index: when another index holds the lease, that parent is skipped without failing the " +
        "batch, because the hourly pass must continue with the next parent.")]
    public async Task hourly_catalogue_parent_skips_when_lease_held()
    {
        // Arrange
        var held = SeedOrganisation();
        var free = SeedOrganisation();
        _locks.SeedMarker(held.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        _discovered = [];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var success = await sut.UpdateParents([held.Id, free.Id], new IndexingContext(ReleasedSince));

        // Assert
        success.Should().BeTrue();
        _catalogueLogger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.AlreadyRunningMessagePrefix) &&
            entry.Message.Contains($"parent-id='{held.Id}'"));
        _locks.IsHeld(held.Id).Should().BeTrue();
        _locks.IsHeld(free.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Hourly TV show index: when another index holds the lease, that show is skipped with the stable AlreadyRunning " +
        "log and the next show is still indexed, because the TV entry point must not duplicate under Busy (#1160).")]
    public async Task hourly_tv_show_skips_when_lease_held()
    {
        // Arrange
        var held = SeedTvShow();
        var free = SeedTvShow();
        _locks.SeedMarker(held.Id, TimeProvider.System.GetUtcNow().UtcDateTime);
        _discovered = [];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var success = await sut.UpdateParents([held.Id, free.Id], new IndexingContext(ReleasedSince));

        // Assert
        success.Should().BeTrue();
        _catalogueLogger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.AlreadyRunningMessagePrefix) &&
            entry.Message.Contains($"parent-id='{held.Id}'") &&
            entry.Message.Contains("kind='TvShow'"));
        _locks.IsHeld(held.Id).Should().BeTrue();
        _locks.IsHeld(free.Id).Should().BeFalse();
    }

    [Fact(DisplayName =
        "ParentIndexingLock.IdFor xor's the parent id with a fixed namespace so the Cosmos id never equals the parent " +
        "id used by the kind-transfer lock.")]
    public void indexing_lock_id_differs_from_parent_id()
    {
        // Arrange
        var parentId = _fixture.CreateGuid();

        // Act
        var lockId = ParentIndexingLock.IdFor(parentId);

        // Assert
        lockId.Should().NotBe(parentId);
        ParentIndexingLock.IdFor(parentId).Should().Be(lockId);
    }

    private Podcast AutoIndexPodcast()
    {
        var podcast = _fixture.CreatePodcast();
        podcast.IndexAllEpisodes = true;
        podcast.Removed = false;
        return podcast;
    }

    private NewsOrganisation SeedOrganisation()
    {
        var source = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        source.IndexAllEpisodes = true;
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(source);
        _organisations[organisation.Id] = organisation;
        return organisation;
    }

    private TvShow SeedTvShow()
    {
        var source = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        source.IndexAllEpisodes = true;
        var show = CatalogueParentKindMapper.ToTvShow(source);
        _shows[show.Id] = show;
        return show;
    }
}
