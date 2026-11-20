using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class D1SearchReconcilerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly D1SearchOptions _options = new();
    private readonly List<(string[] Upserts, string[] Deletes)> _writes = [];
    private Dictionary<string, long> _inventory = [];
    private List<D1SearchCandidate> _catalogue = [];

    public D1SearchReconcilerTests()
    {
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(_options));
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.Read(0, It.IsAny<CancellationToken>()))
            .Returns(() => _catalogue.ToAsyncEnumerable());
        _mocker.GetMock<ID1SearchInventoryReader>()
            .Setup(r => r.ReadInventory(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _inventory);
        var writer = _mocker.GetMock<ID1SearchIndexWriter>();
        writer.SetupGet(w => w.IsActive).Returns(true);
        writer.Setup(w => w.Write(It.IsAny<IReadOnlyCollection<D1SearchDocument>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<D1SearchDocument> u, IReadOnlyCollection<string> d, CancellationToken _) =>
            {
                _writes.Add((u.Select(x => x.Id).ToArray(), d.ToArray()));
                return new D1SearchWriteResult(true, true, u.Count, d.Count, 0, 0, null);
            });
    }

    [Fact(DisplayName =
        "A reconcile counts missing, stale and orphaned documents and writes nothing unless asked to write, because the " +
        "reconcile is a dry run by default and prod D1 is never written without Jon's OK.")]
    public async Task dry_run_reports_drift_and_writes_nothing()
    {
        // Arrange
        var inSync = Candidate(modifiedAt: 10);
        var stale = Candidate(modifiedAt: 20);
        var missing = Candidate(modifiedAt: 30);
        var orphan = _fixture.CreateGuid().ToString();
        _catalogue = [inSync, stale, missing];
        _inventory = new() { [inSync.Id] = 10, [stale.Id] = 19, [orphan] = 5 };
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new
        {
            Outcome = D1SearchReconcileOutcome.DryRun, D1Documents = 3, Eligible = 3, Missing = 1, Stale = 1,
            Orphans = 1, Drift = 1.0
        });
        _writes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A write within the drift threshold upserts only the missing and stale documents and deletes the orphans, " +
        "because re-sending unchanged documents costs a D1 FTS rewrite each.")]
    public async Task write_patches_only_the_drift()
    {
        // Arrange
        _options.Reconcile.DriftThreshold = 0.5;
        var docs = Enumerable.Range(0, 8).Select(_ => Candidate(modifiedAt: 10)).ToList();
        var missing = Candidate(modifiedAt: 10);
        var orphan = _fixture.CreateGuid().ToString();
        _catalogue = [..docs, missing];
        _inventory = docs.ToDictionary(d => d.Id, _ => 10L);
        _inventory[orphan] = 1;
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new
            { Outcome = D1SearchReconcileOutcome.Patched, Missing = 1, Orphans = 1, Upserted = 1, Deleted = 1 });
        _writes.Should().BeEquivalentTo(new[]
        {
            (new[] { missing.Id }, Array.Empty<string>()),
            (Array.Empty<string>(), new[] { orphan })
        });
    }

    [Fact(DisplayName =
        "Drift above the threshold is refused without writing, unless the request allows it, because that much drift " +
        "calls for a rebuild (SP-2.3) or a deliberate backfill, not a quiet weekly patch.")]
    public async Task drift_above_the_threshold_is_refused_unless_allowed()
    {
        // Arrange
        _catalogue = Enumerable.Range(0, 250).Select(_ => Candidate(modifiedAt: 1)).ToList();
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var refused = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);
        var backfill = await sut.Reconcile(new D1SearchReconcileRequest(true, AllowAboveDriftThreshold: true),
            CancellationToken.None);

        // Assert
        refused.Outcome.Should().Be(D1SearchReconcileOutcome.RefusedDrift);
        refused.Succeeded.Should().BeFalse();
        backfill.Should().BeEquivalentTo(new { Outcome = D1SearchReconcileOutcome.Patched, Upserted = 250 });
        _writes.Select(w => w.Upserts.Length).Should().Equal(100, 100, 50);
    }

    [Fact(DisplayName =
        "A playable whose document failed to build is neither written nor deleted, and an excluded one is an orphan, " +
        "because only a known ineligible playable may leave the index.")]
    public async Task build_failures_are_kept_and_excluded_playables_are_orphans()
    {
        // Arrange
        var failed = new D1SearchCandidate(_fixture.CreateGuid().ToString(), null, BuildFailed: true);
        var excluded = new D1SearchCandidate(_fixture.CreateGuid().ToString(), null);
        _catalogue = [failed, excluded];
        _inventory = new() { [failed.Id] = 1, [excluded.Id] = 1 };
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new { Orphans = 1, FailedToBuild = 1, Eligible = 0 });
    }

    [Fact(DisplayName =
        "When the inventory can't be read, the reconcile fails, keeps the eligible count already read from Cosmos, " +
        "and writes nothing, because a partial inventory would make live documents look like orphans and all-zero " +
        "counts would look like a real empty result.")]
    public async Task an_unreadable_inventory_fails_without_writing()
    {
        // Arrange
        _mocker.GetMock<ID1SearchInventoryReader>()
            .Setup(r => r.ReadInventory(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new D1SearchWriteException(_fixture.CreateTitle()));
        _catalogue = [Candidate(modifiedAt: 1)];
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true, true), CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchReconcileOutcome.Failed);
        report.Eligible.Should().Be(1);
        report.FailedToBuild.Should().Be(0);
        report.Error.Should().NotBeNullOrEmpty();
        _writes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the sync is not enabled and configured, the reconcile reads nothing and reports NotConfigured, because a " +
        "switched-off sync must cost nothing and the console must exit non-zero.")]
    public async Task not_configured_reads_nothing()
    {
        // Arrange
        _mocker.GetMock<ID1SearchIndexWriter>().SetupGet(w => w.IsActive).Returns(false);
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchReconcileOutcome.NotConfigured);
        D1SearchReconcileRequest.ExitCode(report).Should().Be(1);
        Reads().Should().Be(0);
        _mocker.GetMock<ID1SearchInventoryReader>().Invocations.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Cosmos is read once to plan, and once more only when writing, because a full scan is the reconcile's biggest " +
        "cost and the weekly timer has a 4.5-minute budget.")]
    public async Task cosmos_is_scanned_once_to_plan_and_once_to_write()
    {
        // Arrange
        _catalogue = [Candidate(modifiedAt: 1)];
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);
        var dryRunReads = Reads();
        await sut.Reconcile(new D1SearchReconcileRequest(true, true), CancellationToken.None);

        // Assert
        dryRunReads.Should().Be(1);
        Reads().Should().Be(3);
        _writes.Should().HaveCount(1);
    }

    [Fact(DisplayName =
        "The console refuses --allow-drift without --write, because allowing drift only means something when writing.")]
    public void console_allow_drift_needs_write()
    {
        // Arrange
        const bool write = false;

        // Act
        var request = D1SearchReconcileRequest.FromCommandLine(write, true, out var error);

        // Assert
        request.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName =
        "Without --write the console asks for a dry run, because reading is always safe.")]
    public void console_defaults_to_a_dry_run()
    {
        // Arrange
        const bool write = false;

        // Act
        var request = D1SearchReconcileRequest.FromCommandLine(write, false, out var error);

        // Assert
        error.Should().BeNull();
        request.Should().Be(new D1SearchReconcileRequest(false));
    }

    private int Reads() => _mocker.GetMock<ID1SearchCatalogue>().Invocations.Count(i => i.Method.Name == nameof(ID1SearchCatalogue.Read));

    private D1SearchCandidate Candidate(long modifiedAt)
    {
        var id = _fixture.CreateGuid().ToString();
        return new D1SearchCandidate(id, new D1SearchDocument
        {
            Id = id,
            Title = _fixture.CreateTitle(),
            Release = DateTimeOffset.UtcNow,
            Duration = "00:10:00",
            ModifiedAt = modifiedAt
        });
    }
}
