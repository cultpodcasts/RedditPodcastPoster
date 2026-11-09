using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// A podcast's budget token and stage reporter live on its <see cref="PodcastRunScope"/>, not on the
/// <see cref="IndexingContext"/> policy record (#1073): each belongs to one podcast's run only.
/// </summary>
public class PodcastRunScopeRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(7);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly CancellationTokenSource _budget = new();

    [Fact(DisplayName =
        "Outside an indexer run there is no budget and no stage reporter, so submit, discovery and console callers " +
        "keep running uncancelled and reporting a stage is a no-op.")]
    public void outside_a_run_the_scope_is_none()
    {
        // Arrange
        var stage = _fixture.Create<string>();

        // Act
        var scope = PodcastRunScope.Current;
        var report = () => scope.ReportStage(stage);

        // Assert
        scope.Should().BeSameAs(PodcastRunScope.None);
        scope.CancellationToken.CanBeCanceled.Should().BeFalse();
        scope.StageReporter.Should().BeNull();
        report.Should().NotThrow();
    }

    [Fact(DisplayName =
        "Work awaited inside a podcast's run sees that podcast's budget token and reports stages to its reporter, " +
        "because the indexer must cancel and trace the calls the podcast's update makes.")]
    public async Task awaited_work_inside_a_run_sees_its_scope()
    {
        // Arrange
        var reporter = _mocker.GetMock<IPodcastIndexingStageReporter>();
        var stage = _fixture.Create<string>();
        var detail = _fixture.Create<string>();
        using var entered = PodcastRunScope.Enter(new PodcastRunScope(_budget.Token, reporter.Object));

        // Act
        var seen = await Task.Run(async () =>
        {
            await Task.Yield();
            PodcastRunScope.Current.ReportStage(stage, detail);
            return PodcastRunScope.Current.CancellationToken;
        });

        // Assert
        seen.Should().Be(_budget.Token);
        reporter.Verify(x => x.Stage(stage, detail), Times.Once);
    }

    [Fact(DisplayName =
        "Leaving a podcast's run restores the scope that was current before, even when runs nest, so the next podcast " +
        "never inherits the previous podcast's token or reporter.")]
    public void leaving_a_run_restores_the_previous_scope()
    {
        // Arrange
        using var inner = new CancellationTokenSource();
        var outerScope = new PodcastRunScope(_budget.Token);
        var innerScope = new PodcastRunScope(inner.Token);

        // Act
        PodcastRunScope duringInner;
        PodcastRunScope afterInner;
        PodcastRunScope afterOuter;
        using (PodcastRunScope.Enter(outerScope))
        {
            using (PodcastRunScope.Enter(innerScope))
            {
                duringInner = PodcastRunScope.Current;
            }

            afterInner = PodcastRunScope.Current;
        }

        afterOuter = PodcastRunScope.Current;

        // Assert
        duringInner.Should().BeSameAs(innerScope);
        afterInner.Should().BeSameAs(outerScope);
        afterOuter.Should().BeSameAs(PodcastRunScope.None);
    }

    [Fact(DisplayName =
        "An update that is still running after the loop has left its run keeps its own (cancelled) budget token, while " +
        "the loop is back outside any run, because an abandoned update must keep observing its cut-off and must not " +
        "hand its token to the next podcast.")]
    public async Task an_update_outliving_its_run_keeps_its_own_scope()
    {
        // Arrange
        var release = new TaskCompletionSource();
        var podcastScope = new PodcastRunScope(_budget.Token);
        Task<CancellationToken> update;
        using (PodcastRunScope.Enter(podcastScope))
        {
            update = ReadScopeAfter(release.Task);
        }

        // Act
        var loopScope = PodcastRunScope.Current;
        await _budget.CancelAsync();
        release.SetResult();
        var updateToken = await update;

        // Assert
        loopScope.Should().BeSameAs(PodcastRunScope.None);
        updateToken.Should().Be(_budget.Token);
        updateToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A podcast's policy copy is value-equal to the batch policy it was cloned from and its text shows only policy, " +
        "whatever run scope it is used under, because the budget token and stage reporter are not indexing policy.")]
    public void indexing_policy_copy_is_unaffected_by_the_run_scope()
    {
        // Arrange
        var batch = new IndexingContext(ReleasedSince);
        var reporter = _mocker.GetMock<IPodcastIndexingStageReporter>();

        // Act
        IndexingContext podcastCopy;
        using (PodcastRunScope.Enter(new PodcastRunScope(_budget.Token, reporter.Object)))
        {
            podcastCopy = batch.ForPodcastUpdate();
        }

        // Assert
        podcastCopy.Should().NotBeSameAs(batch);
        podcastCopy.Should().Be(batch);
        podcastCopy.GetHashCode().Should().Be(batch.GetHashCode());
        podcastCopy.ToString().Should().Be(batch.ToString());
    }

    private static async Task<CancellationToken> ReadScopeAfter(Task gate)
    {
        await gate;
        return PodcastRunScope.Current.CancellationToken;
    }
}
