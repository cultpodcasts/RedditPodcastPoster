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
    private const string Preview = "preview";
    private const string Production = "production";
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly D1SearchOptions _options = new();
    private readonly Dictionary<string, Dictionary<string, long>> _inventories = new() { [Production] = [], [Preview] = [] };
    private readonly List<(string Target, string[] Upserts, string[] Deletes)> _writes = [];
    private List<D1SearchCandidate> _catalogue = [];

    public D1SearchReconcilerTests()
    {
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(_options));
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.Read(0, It.IsAny<CancellationToken>()))
            .Returns(() => _catalogue.ToAsyncEnumerable());
        _mocker.GetMock<ID1SearchInventoryReader>()
            .Setup(r => r.ReadInventory(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string target, CancellationToken _) => _inventories[target]);
        var writer = _mocker.GetMock<ID1SearchIndexWriter>();
        writer.SetupGet(w => w.UsableTargets).Returns([Production, Preview]);
        writer.Setup(w => w.Write(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<D1SearchDocument>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string target, IReadOnlyCollection<D1SearchDocument> u, IReadOnlyCollection<string> d,
                CancellationToken _) =>
            {
                _writes.Add((target, u.Select(x => x.Id).ToArray(), d.ToArray()));
                return new D1SearchWriteResult([new D1SearchTargetOutcome(target, true, u.Count, d.Count, 0, 0, null)]);
            });
    }

    [Fact(DisplayName =
        "A reconcile counts missing, stale and orphaned documents per target and writes nothing unless the target is a " +
        "write target, because the reconcile is a dry run by default and prod D1 is never written without Jon's OK.")]
    public async Task dry_run_reports_drift_and_writes_nothing()
    {
        // Arrange
        var inSync = Candidate(modifiedAt: 10);
        var stale = Candidate(modifiedAt: 20);
        var missing = Candidate(modifiedAt: 30);
        var orphan = _fixture.CreateGuid().ToString();
        _catalogue = [inSync, stale, missing];
        _inventories[Production] = new() { [inSync.Id] = 10, [stale.Id] = 19, [orphan] = 5 };
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var reports = await sut.Reconcile(new D1SearchReconcileRequest([Production], []), CancellationToken.None);

        // Assert
        reports.Single().Should().BeEquivalentTo(new
        {
            Target = Production, Outcome = D1SearchReconcileOutcome.DryRun, D1Documents = 3, Eligible = 3, Missing = 1,
            Stale = 1, Orphans = 1, Drift = 1.0
        });
        _writes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A write target within the drift threshold gets only its missing and stale documents upserted and its orphans " +
        "deleted, because re-sending unchanged documents costs a D1 FTS rewrite each.")]
    public async Task write_target_patches_only_the_drift()
    {
        // Arrange
        _options.Reconcile.DriftThreshold = 0.5;
        var docs = Enumerable.Range(0, 8).Select(_ => Candidate(modifiedAt: 10)).ToList();
        var missing = Candidate(modifiedAt: 10);
        var orphan = _fixture.CreateGuid().ToString();
        _catalogue = [..docs, missing];
        _inventories[Preview] = docs.ToDictionary(d => d.Id, _ => 10L);
        _inventories[Preview][orphan] = 1;
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var reports = await sut.Reconcile(new D1SearchReconcileRequest([Preview], [Preview]), CancellationToken.None);

        // Assert
        reports.Single().Should().BeEquivalentTo(new
            { Outcome = D1SearchReconcileOutcome.Patched, Missing = 1, Orphans = 1, Upserted = 1, Deleted = 1 });
        _writes.Should().BeEquivalentTo(new[]
        {
            (Preview, new[] { missing.Id }, Array.Empty<string>()),
            (Preview, Array.Empty<string>(), new[] { orphan })
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
        var refused = await sut.Reconcile(new D1SearchReconcileRequest([Preview], [Preview]), CancellationToken.None);
        var backfill = await sut.Reconcile(new D1SearchReconcileRequest([Preview], [Preview], AllowAboveDriftThreshold: true),
            CancellationToken.None);

        // Assert
        refused.Single().Outcome.Should().Be(D1SearchReconcileOutcome.RefusedDrift);
        refused.Single().Succeeded.Should().BeFalse();
        backfill.Single().Should().BeEquivalentTo(new { Outcome = D1SearchReconcileOutcome.Patched, Upserted = 250 });
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
        _inventories[Production] = new() { [failed.Id] = 1, [excluded.Id] = 1 };
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = (await sut.Reconcile(new D1SearchReconcileRequest([Production], []), CancellationToken.None)).Single();

        // Assert
        report.Should().BeEquivalentTo(new { Orphans = 1, FailedToBuild = 1, Eligible = 0 });
    }

    [Fact(DisplayName =
        "When a target's inventory can't be read, that target fails and nothing is written, because a partial " +
        "inventory would make live documents look like orphans.")]
    public async Task an_unreadable_inventory_fails_the_target_without_writing()
    {
        // Arrange
        _mocker.GetMock<ID1SearchInventoryReader>()
            .Setup(r => r.ReadInventory(Production, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new D1SearchWriteException(_fixture.CreateTitle()));
        _catalogue = [Candidate(modifiedAt: 1)];
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var reports = await sut.Reconcile(new D1SearchReconcileRequest(null, [Production, Preview], true), CancellationToken.None);

        // Assert
        reports.Select(r => (r.Target, r.Outcome)).Should().Equal(
            (Production, D1SearchReconcileOutcome.Failed), (Preview, D1SearchReconcileOutcome.Patched));
        _writes.Should().OnlyContain(w => w.Target == Preview);
    }

    [Fact(DisplayName =
        "Cosmos is read once to plan however many targets there are, and once more only when one of them writes, " +
        "because a scan per target would not fit the weekly timer's budget and doubles the Cosmos cost.")]
    public async Task cosmos_is_scanned_once_to_plan_and_once_to_write()
    {
        // Arrange
        _catalogue = [Candidate(modifiedAt: 1)];
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        await sut.Reconcile(new D1SearchReconcileRequest(null, []), CancellationToken.None);
        var dryRunReads = Reads();
        await sut.Reconcile(new D1SearchReconcileRequest(null, [Production, Preview], true), CancellationToken.None);

        // Assert
        dryRunReads.Should().Be(1);
        Reads().Should().Be(3);
        _writes.Select(w => w.Target).Should().BeEquivalentTo([Production, Preview]);
    }

    private int Reads() => _mocker.GetMock<ID1SearchCatalogue>().Invocations.Count(i => i.Method.Name == nameof(ID1SearchCatalogue.Read));

    [Theory(DisplayName =
        "The console needs --target with --write and --write with --allow-drift, because a write to the production " +
        "index must always be named on the command line.")]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public void console_command_line_rules(bool write, bool allowDrift, bool named)
    {
        // Arrange
        string[] targets = named ? [Preview] : [];

        // Act
        var request = D1SearchReconcileRequest.FromCommandLine(targets, write, allowDrift, out var error);

        // Assert
        request.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName =
        "Without --write the console asks for a dry run of the named targets, because reading is always safe.")]
    public void console_defaults_to_a_dry_run()
    {
        // Arrange
        string[] targets = [Production];

        // Act
        var request = D1SearchReconcileRequest.FromCommandLine(targets, false, false, out var error);

        // Assert
        error.Should().BeNull();
        request.Should().BeEquivalentTo(new { Targets = new[] { Production }, WriteTargets = Array.Empty<string>() });
    }

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
