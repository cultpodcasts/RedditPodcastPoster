using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The on-demand <see cref="Indexer"/> hands its budget token to the podcast's update through the ambient
/// <see cref="PodcastRunScope"/> (#1073). Nothing at compile time ties the two together, so these rules pin it: the
/// update runs inside a cancellable scope that the on-demand budget cancels, and the indexer's own follow-up work runs
/// outside it.
/// </summary>
public class IndexerOnDemandRunScopeRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<Indexer> _logger = new();
    private readonly PodcastUpdateBudgetOptions _budget = new();
    private readonly Podcast _podcast;
    private readonly Episode _added;
    private Func<Task<IndexPodcastResult>> _update;
    private PodcastRunScope? _updateScope;
    private PodcastRunScope? _subjectEnrichmentScope;

    public IndexerOnDemandRunScopeRules()
    {
        var podcastRepository = new InMemoryPodcastRepository();
        _podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _podcast.IndexAllEpisodes = true;
        podcastRepository.Seed(_podcast);
        _added = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        _added.Id = _fixture.CreateGuid();
        _added.PodcastId = _podcast.Id;
        _update = () => Task.FromResult(Result());

        _mocker.Use<IPodcastRepository>(podcastRepository);
        _mocker.Use<IEpisodeRepository>(new InMemoryEpisodeRepository());
        _mocker.Use<IParentIndexingLockStore>(new InMemoryParentIndexingLockStore());
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use<ILogger<Indexer>>(_logger);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(_budget));

        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), It.IsAny<bool>(), It.IsAny<IndexingContext>()))
            .Returns(() =>
            {
                // The update reads the scope the way platform leaves do: from the ambient run scope.
                _updateScope = PodcastRunScope.Current;
                return _update();
            });
        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .Callback(() => _subjectEnrichmentScope = PodcastRunScope.Current)
            .ReturnsAsync(new EnrichSubjectsResult([], []));
        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));
        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(It.IsAny<Podcast>(), It.IsAny<Episode?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));
    }

    [Fact(DisplayName =
        "On-demand podcast index: the podcast's update runs inside a run scope whose token can be cancelled and is not " +
        "yet cancelled, and subject enrichment after the update runs outside any run, because the on-demand budget " +
        "must reach every call the update makes and must not leak into the indexer's follow-up work.")]
    public async Task on_demand_update_runs_inside_a_cancellable_run_scope()
    {
        // Arrange
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await sut.Index(_podcast.Id, new IndexingContext(ReleasedSince))
            .WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.Performed);
        _updateScope.Should().NotBeNull();
        _updateScope.Should().NotBeSameAs(PodcastRunScope.None);
        _updateScope!.CancellationToken.CanBeCanceled.Should().BeTrue();
        _updateScope.CancellationToken.IsCancellationRequested.Should().BeFalse();
        _subjectEnrichmentScope.Should().BeSameAs(PodcastRunScope.None);
    }

    [Fact(DisplayName =
        "On-demand podcast index: when the update waits on its run scope's token, the on-demand budget cancels that " +
        "token, the update stops, and the answer is TimedOut without abandoning it, because the budget must cancel " +
        "the update itself rather than only stop waiting for it.")]
    public async Task on_demand_budget_cancels_the_update_through_its_run_scope()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _update = async () =>
        {
            var token = PodcastRunScope.Current.CancellationToken;
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Result();
        };
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var run = sut.Index(_podcast.Id, new IndexingContext(ReleasedSince));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _time.Advance(_budget.OnDemandBudget);
        var response = await run.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.TimedOut);
        _updateScope!.CancellationToken.IsCancellationRequested.Should().BeTrue();
        _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(Indexer.BudgetExceededMessagePrefix) &&
            entry.Message.Contains("abandoned='False'"));
    }

    private IndexPodcastResult Result() =>
        new(
            _podcast,
            new EpisodeMergeResult([], [_added], [], []),
            new FilterResult([]),
            new EnrichmentResults([]),
            SpotifyBypassed: false,
            YouTubeBypassed: false);
}
