using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Migrated news organisations and TV shows run under the same per-podcast time budget as podcasts (#1070), so one
/// slow parent cannot hang the indexer activity.
/// </summary>
public class CatalogueParentIndexingBudgetRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);
    private static readonly TimeSpan PostableDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration + TimeSpan.FromMinutes(5);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<CatalogueParentsUpdater> _logger = new();
    private readonly PodcastUpdateBudgetOptions _budget = new();
    private readonly PodcastIndexingStageLogOptions _stageLog = new();
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<(Guid Id, CatalogueParentIndexerPatch Patch)> _patches = [];
    private readonly Dictionary<Guid, IndexingContext> _contexts = [];
    private readonly NewsOrganisation _slowOrganisation;
    private readonly NewsOrganisation _healthyOrganisation;
    private Func<IndexingContext, Task<IList<Episode>>> _slowDiscovery;
    private IList<Episode> _discovered = [];

    public CatalogueParentIndexingBudgetRules()
    {
        _slowOrganisation = SeedOrganisation();
        _healthyOrganisation = SeedOrganisation();
        _slowDiscovery = _ => Task.FromResult<IList<Episode>>([]);

        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use<ILogger<CatalogueParentsUpdater>>(_logger);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(_budget));
        _mocker.Use<IOptions<PodcastIndexingStageLogOptions>>(Options.Create(_stageLog));
        _mocker.Use(new PodcastUpdateBudgetCutTracker());

        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<RedditPodcastPoster.Models.TvShows.TvShow, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<RedditPodcastPoster.Models.TvShows.TvShow>());
        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .Callback((Guid id, CatalogueParentIndexerPatch patch) => _patches.Add((id, patch)))
            .ReturnsAsync(true);
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports.Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<NewsReport>());
        reports.Setup(x => x.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback((IEnumerable<NewsReport> items) => _savedReports.AddRange(items))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Returns((Podcast podcast, IEnumerable<Episode> _, IndexingContext context) =>
            {
                _contexts[podcast.Id] = context;
                return podcast.Id == _slowOrganisation.Id
                    ? _slowDiscovery(context)
                    : Task.FromResult<IList<Episode>>(_discovered.ToList());
            });
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> _, IEnumerable<Episode> incoming) =>
                new EpisodeMergeResult(incoming.ToList(), incoming.ToList(), [], []));
        _mocker.GetMock<IPodcastFilter>()
            .Setup(x => x.Filter(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<List<string>>()))
            .Returns(new FilterResult([]));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<Podcast>());
        _mocker.GetMock<IEliminationTermsProvider>()
            .Setup(x => x.GetEliminationTerms())
            .Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync())
            .ReturnsAsync(() => _mocker.GetMock<IEliminationTermsProvider>().Object);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation's discovery is still waiting when its budget elapses, then " +
        "it is cancelled, the budget log names the parent and the stage it was stuck in, nothing is written for it, and the " +
        "next parent is still indexed, because one slow news organisation must not hang the indexer.")]
    public async Task slow_news_organisation_is_cut_and_the_next_parent_is_indexed()
    {
        // Arrange
        _slowDiscovery = async context =>
        {
            context.ReportStage("youtube-playlist-page", "page=2");
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return [];
        };
        _discovered = [NewVideo()];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var run = sut.UpdateParents([_slowOrganisation.Id, _healthyOrganisation.Id], Context());
        _time.Advance(Budget);
        var success = await Completion(run);

        // Assert
        success.Should().BeFalse("a parent cut off by its budget is a failed update");
        var budgetLog = _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.BudgetExceededMessagePrefix)).Subject;
        budgetLog.Level.Should().Be(LogLevel.Error);
        budgetLog.Message.Should().Contain($"parent-id='{_slowOrganisation.Id}'")
            .And.Contain("kind='NewsOrganisation'")
            .And.Contain("stage='youtube-playlist-page'")
            .And.Contain("detail='page=2'")
            .And.Contain("abandoned='False'")
            .And.Contain("consecutive-cuts='1'");
        _savedReports.Should().OnlyContain(report => report.NewsOrganisationId == _healthyOrganisation.Id)
            .And.ContainSingle();
        _patches.Select(patch => patch.Id).Should().Equal(_healthyOrganisation.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a parent's discovery ignores its cancelled token, then it is abandoned after the " +
        "grace period, the next parent is indexed, and when the abandoned discovery later returns it writes nothing, " +
        "because a call that never observes cancellation must neither hold up the pass nor write late.")]
    public async Task parent_ignoring_cancellation_is_abandoned_and_writes_nothing_late()
    {
        // Arrange
        var late = new TaskCompletionSource<IList<Episode>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _slowDiscovery = _ => late.Task;
        _discovered = [NewVideo()];
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var run = sut.UpdateParents([_slowOrganisation.Id, _healthyOrganisation.Id], Context());
        _time.Advance(Budget);
        _time.Advance(TimeSpan.FromSeconds(_budget.AbandonGraceSeconds));
        var success = await Completion(run);
        late.SetResult([NewVideo()]);
        await WaitFor(() => _logger.Entries.Any(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.AbandonedCompletedMessagePrefix)));

        // Assert
        success.Should().BeFalse();
        _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(CatalogueParentsUpdater.BudgetExceededMessagePrefix) &&
            entry.Message.Contains($"parent-id='{_slowOrganisation.Id}'") &&
            entry.Message.Contains("abandoned='True'"));
        _savedReports.Should().ContainSingle().Which.NewsOrganisationId.Should().Be(_healthyOrganisation.Id);
        _patches.Select(patch => patch.Id).Should().Equal(_healthyOrganisation.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: each parent's discovery gets a cancellable token and a stage reporter, and its start " +
        "(with the parent kind) and completion are logged at Information under the IndexerPodcastStage prefix, because a " +
        "stalled parent must be traceable with the same App Insights queries as a podcast.")]
    public async Task parent_update_carries_token_and_logs_stages()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var success = await sut.UpdateParents([_healthyOrganisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        var context = _contexts[_healthyOrganisation.Id];
        context.CancellationToken.CanBeCanceled.Should().BeTrue();
        context.StageReporter.Should().BeOfType<PodcastIndexingStageLog>();
        var stages = _logger.Entries
            .Where(entry => entry.Message.StartsWith(PodcastIndexingStageLog.MessagePrefix) &&
                            entry.Message.Contains($"podcast-id='{_healthyOrganisation.Id}'"))
            .ToList();
        stages.Should().OnlyContain(entry => entry.Level == LogLevel.Information);
        stages.Should().Contain(entry =>
            entry.Message.Contains("stage='start'") && entry.Message.Contains("catalogue-parent-kind=NewsOrganisation"));
        stages.Should().Contain(entry => entry.Message.Contains("stage='discover-episodes'"));
        stages.Should().Contain(entry => entry.Message.Contains("stage='complete'"));
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a stored parent still has empty-string indexing text from before blanks were " +
        "stored as null, then the pass does not write those members back, because a normalisation-only write could " +
        "overwrite a curator edit saved mid-pass.")]
    public async Task legacy_empty_indexing_text_is_not_patched()
    {
        // Arrange
        var indexing = _healthyOrganisation.Indexing!;
        indexing.TitleRegex = string.Empty;
        indexing.DescriptionRegex = string.Empty;
        indexing.YouTubePlaylistId = string.Empty;
        var sut = _mocker.CreateInstance<CatalogueParentsUpdater>();

        // Act
        var success = await sut.UpdateParents([_healthyOrganisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _patches.Should().ContainSingle().Which.Patch.ChangedIndexingProperties.Should().BeEmpty();
    }

    private TimeSpan Budget => TimeSpan.FromSeconds(_budget.PerPodcastBudgetSeconds);

    private static IndexingContext Context() => new(ReleasedSince);

    private static Task<bool> Completion(Task<bool> run) =>
        // Real time only bounds a hung test; fake time drives the budget.
        run.WaitAsync(TimeSpan.FromSeconds(10));

    private static async Task WaitFor(Func<bool> condition)
    {
        // Real time only bounds a hung test: the abandoned update finishes on the thread pool.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private Episode NewVideo() =>
        _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));

    private NewsOrganisation SeedOrganisation()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.IndexAllEpisodes = true;
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        _organisations[organisation.Id] = organisation;
        return organisation;
    }
}
