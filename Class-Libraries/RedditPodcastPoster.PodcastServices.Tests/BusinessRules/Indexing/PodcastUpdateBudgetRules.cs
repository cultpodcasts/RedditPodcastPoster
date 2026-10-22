using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The indexer gives each podcast a time budget so one podcast whose calls stall cannot consume the whole activity
/// (a pass was killed at the 5-minute function timeout while one podcast's update sat idle).
/// </summary>
public class PodcastUpdateBudgetRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(7);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<PodcastsUpdater> _logger = new();
    private readonly InMemoryPodcastRepository _repository = new();
    private readonly PodcastUpdateBudgetOptions _budget = new();
    private readonly List<Guid> _updated = [];
    private readonly Podcast _stalledPodcast;
    private readonly Podcast _healthyPodcast;
    private Func<IndexingContext, Task<IndexPodcastResult>> _stalledUpdate;

    public PodcastUpdateBudgetRules()
    {
        _stalledPodcast = AutoIndexPodcast();
        _healthyPodcast = AutoIndexPodcast();
        _repository.Seed(_stalledPodcast);
        _repository.Seed(_healthyPodcast);
        _stalledUpdate = _ => Task.FromResult(SuccessResult(_stalledPodcast));

        _mocker.Use<IPodcastRepository>(_repository);
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use<ILogger<PodcastsUpdater>>(_logger);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(_budget));
        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .Returns((Podcast podcast, bool _, IndexingContext context) =>
            {
                _updated.Add(podcast.Id);
                return podcast.Id == _stalledPodcast.Id
                    ? _stalledUpdate(context)
                    : Task.FromResult(SuccessResult(podcast));
            });
    }

    [Fact(DisplayName =
        "When a podcast's update is still waiting when its budget elapses, the update is cancelled, the budget log names " +
        "the podcast and the stage it was stuck in, and the next podcast is still indexed, because one stalled podcast " +
        "must not cost the rest of the pass.")]
    public async Task stalled_podcast_is_cancelled_and_the_loop_continues()
    {
        // Arrange
        _stalledUpdate = async context =>
        {
            context.ReportStage("youtube-playlist-page", "page=2");
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return SuccessResult(_stalledPodcast);
        };
        var sut = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var success = await RunAdvancingTime(sut.UpdatePodcasts(
            [_stalledPodcast.Id, _healthyPodcast.Id], new IndexingContext(ReleasedSince)));

        // Assert
        success.Should().BeFalse("a podcast cut off by its budget is a failed update");
        _updated.Should().BeEquivalentTo([_stalledPodcast.Id, _healthyPodcast.Id]);
        var budgetLog = _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(PodcastsUpdater.BudgetExceededMessagePrefix)).Subject;
        budgetLog.Level.Should().Be(LogLevel.Error);
        budgetLog.Message.Should().Contain($"podcast-id='{_stalledPodcast.Id}'")
            .And.Contain("stage='youtube-playlist-page'")
            .And.Contain("detail='page=2'")
            .And.Contain("abandoned='False'");
    }

    [Fact(DisplayName =
        "When a podcast's update ignores its cancelled token, the loop abandons it after the grace period and indexes " +
        "the next podcast, because a call that never observes cancellation must still not hold up the pass.")]
    public async Task update_ignoring_cancellation_is_abandoned_after_grace()
    {
        // Arrange
        _stalledUpdate = _ => new TaskCompletionSource<IndexPodcastResult>().Task;
        var sut = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var success = await RunAdvancingTime(sut.UpdatePodcasts(
            [_stalledPodcast.Id, _healthyPodcast.Id], new IndexingContext(ReleasedSince)));

        // Assert
        success.Should().BeFalse();
        _updated.Should().Contain(_healthyPodcast.Id);
        _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(PodcastsUpdater.BudgetExceededMessagePrefix) &&
            entry.Message.Contains($"podcast-id='{_stalledPodcast.Id}'") &&
            entry.Message.Contains("abandoned='True'"));
    }

    [Fact(DisplayName =
        "Each podcast update gets a cancellable token and a stage reporter, and its start and completion are logged at " +
        "Warning with the podcast id, because the indexer only ships Warning logs and a stall must be traceable.")]
    public async Task podcast_update_carries_token_and_logs_stages()
    {
        // Arrange
        IndexingContext? seen = null;
        _stalledUpdate = context =>
        {
            seen = context;
            return Task.FromResult(SuccessResult(_stalledPodcast));
        };
        var sut = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var success = await sut.UpdatePodcasts([_stalledPodcast.Id], new IndexingContext(ReleasedSince));

        // Assert
        success.Should().BeTrue();
        seen!.CancellationToken.CanBeCanceled.Should().BeTrue();
        seen.StageReporter.Should().NotBeNull();
        var stageLines = _logger.Entries
            .Where(entry => entry.Message.StartsWith(PodcastIndexingStageLog.MessagePrefix))
            .ToList();
        stageLines.Should().HaveCount(2);
        stageLines.Should().OnlyContain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains($"podcast-id='{_stalledPodcast.Id}'"));
        stageLines[0].Message.Should().Contain("stage='start'");
        stageLines[1].Message.Should().Contain("stage='complete'");
    }

    [Fact(DisplayName =
        "A zero per-podcast budget disables the cut-off, so a slow update is awaited to completion.")]
    public async Task zero_budget_disables_the_cut_off()
    {
        // Arrange
        _budget.PerPodcastBudgetSeconds = 0;
        var release = new TaskCompletionSource<IndexPodcastResult>();
        _stalledUpdate = _ => release.Task;
        var sut = _mocker.CreateInstance<PodcastsUpdater>();

        // Act
        var run = sut.UpdatePodcasts([_stalledPodcast.Id], new IndexingContext(ReleasedSince));
        _time.Advance(TimeSpan.FromSeconds(PodcastUpdateBudgetOptions.DefaultPerPodcastBudgetSeconds * 10));
        var completedBeforeRelease = run.IsCompleted;
        release.SetResult(SuccessResult(_stalledPodcast));
        var success = await run.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        completedBeforeRelease.Should().BeFalse();
        success.Should().BeTrue();
        _logger.Entries.Should().NotContain(entry =>
            entry.Message.StartsWith(PodcastsUpdater.BudgetExceededMessagePrefix));
    }

    private async Task<bool> RunAdvancingTime(Task<bool> run)
    {
        // Advance fake time in small steps until the loop finishes; real time only bounds a hung test.
        for (var step = 0; step < 200 && !run.IsCompleted; step++)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(10);
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(10));
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
