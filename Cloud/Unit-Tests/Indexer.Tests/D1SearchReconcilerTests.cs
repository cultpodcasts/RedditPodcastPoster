using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Search;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
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
            .Setup(r => r.ReadInventory(It.IsAny<CancellationToken>(), It.IsAny<IProgress<D1SearchReconcileProgress>?>()))
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
            .Setup(r => r.ReadInventory(It.IsAny<CancellationToken>(), It.IsAny<IProgress<D1SearchReconcileProgress>?>()))
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

    [Fact(DisplayName =
        "A dry run reports starting, the first catalogue item, then every ten, and inventory as a running " +
        "count, and never upsert or delete, because the bar must move during the first Cosmos page and the " +
        "inventory token, not only every hundred documents or a dummy 0/1 request.")]
    public async Task dry_run_reports_read_progress_only()
    {
        // Arrange
        _catalogue = Enumerable.Range(0, 250).Select(_ => Candidate(modifiedAt: 1)).ToList();
        var progress = new CollectingProgress();
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None, progress);

        // Assert
        var catalogue = new List<D1SearchReconcileProgress>
        {
            new(D1SearchReconcilePhase.Catalogue, 0, Detail: "waiting for first page"),
            new(D1SearchReconcilePhase.Catalogue, 1)
        };
        for (var n = D1SearchReconciler.CatalogueProgressInterval; n <= 250; n += D1SearchReconciler.CatalogueProgressInterval)
        {
            catalogue.Add(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, n));
        }

        catalogue.Add(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, 250, 250));
        progress.Items.Should().Equal(
            [
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Starting, 0, 1, "reading watermark"),
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Starting, 1, 1, "full: no watermark yet"),
                .. catalogue,
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Inventory, 0),
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Inventory, 0, 0)
            ]);
        progress.Items.Should().NotContain(p =>
            p.Phase == D1SearchReconcilePhase.Upsert || p.Phase == D1SearchReconcilePhase.Delete);
    }

    [Fact(DisplayName =
        "A write reports upsert progress after each flush and delete progress after each chunk, against the planned " +
        "totals, because the backfill needs a running count and an ETA.")]
    public async Task write_reports_upsert_and_delete_progress()
    {
        // Arrange
        _catalogue = Enumerable.Range(0, 250).Select(_ => Candidate(modifiedAt: 1)).ToList();
        var orphans = Enumerable.Range(0, 3).Select(_ => _fixture.CreateGuid().ToString()).ToList();
        _inventory = orphans.ToDictionary(id => id, _ => 1L);
        var progress = new CollectingProgress();
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true, true), CancellationToken.None, progress);

        // Assert
        report.Should().BeEquivalentTo(new { Outcome = D1SearchReconcileOutcome.Patched, Upserted = 250, Deleted = 3 });
        progress.Items.Should().Contain(new D1SearchReconcileProgress(
            D1SearchReconcilePhase.Upsert, 0, 250, "waiting for first page"));
        progress.Items.Should().Contain(new D1SearchReconcileProgress(
            D1SearchReconcilePhase.Upsert, 0, 250, "scanned 1"));
        progress.Items.Where(p => p.Phase == D1SearchReconcilePhase.Upsert && p.Detail is null)
            .Should().Equal(
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Upsert, 100, 250),
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Upsert, 200, 250),
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Upsert, 250, 250));
        progress.Items.Where(p => p.Phase == D1SearchReconcilePhase.Delete)
            .Should().Equal(
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Delete, 0, 3),
                new D1SearchReconcileProgress(D1SearchReconcilePhase.Delete, 3, 3));
    }


    [Fact(DisplayName =
        "The console accepts --full with a dry run or a write, because a full compare is a safety option, not a write switch.")]
    public void console_accepts_full()
    {
        // Arrange
        const bool write = false;

        // Act
        var request = D1SearchReconcileRequest.FromCommandLine(write, false, true, out var error);

        // Assert
        error.Should().BeNull();
        request.Should().Be(new D1SearchReconcileRequest(false, Full: true));
    }

    [Fact(DisplayName =
        "When a watermark has been saved and --full is not set, the reconcile reads the incremental catalogue and " +
        "the eligible-id snapshot, because day-to-day runs must not rebuild every search document.")]
    public async Task incremental_reads_the_window_and_eligible_ids()
    {
        // Arrange
        const long since = 1_700_000_000;
        var changed = Candidate(modifiedAt: since * 1000);
        _catalogue = [changed];
        _inventory = new() { [changed.Id] = since * 1000 };
        SetupIncremental(since, [changed.Id]);
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchReconcileOutcome.InSync);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.ReadIncremental(since, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.ReadEligibleIds(It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.Read(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.SaveAsync(It.IsAny<D1SearchReconcileWatermark>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "An incremental write upserts from the documents already built in the window and does not stream the " +
        "catalogue a second time, because the changed set is small and a second full scan is the cost we are dropping.")]
    public async Task incremental_write_does_not_rescan_the_catalogue()
    {
        // Arrange
        const long since = 1_700_000_000;
        var missing = Candidate(modifiedAt: (since + 10) * 1000);
        var orphan = _fixture.CreateGuid().ToString();
        _catalogue = [missing];
        _inventory = new() { [orphan] = 1 };
        SetupIncremental(since, []);
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true, true), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new
            { Outcome = D1SearchReconcileOutcome.Patched, Missing = 1, Orphans = 1, Upserted = 1, Deleted = 1 });
        _writes.Should().BeEquivalentTo(new[]
        {
            (new[] { missing.Id }, Array.Empty<string>()),
            (Array.Empty<string>(), new[] { orphan })
        });
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.Read(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.ReadIncremental(since, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.SaveAsync(It.Is<D1SearchReconcileWatermark>(w => w.HighWaterMark == since + 10),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A successful write advances the watermark to the highest Cosmos _ts considered, and a dry run, a refused " +
        "drift, or a failed write leaves it where it was, because a later incremental run must re-read a window that " +
        "did not finish.")]
    public async Task watermark_advances_only_after_a_successful_write()
    {
        // Arrange
        const long ts = 1_700_000_000;
        var candidate = Candidate(modifiedAt: ts * 1000);
        _catalogue = [candidate];
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);
        _inventory = new() { [candidate.Id] = ts * 1000 };
        await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);
        _inventory = [];
        _mocker.GetMock<ID1SearchIndexWriter>()
            .Setup(w => w.Write(It.IsAny<IReadOnlyCollection<D1SearchDocument>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new D1SearchWriteResult(false, false, 0, 0, 0, 0, "write failed"));
        await sut.Reconcile(new D1SearchReconcileRequest(true, true), CancellationToken.None);

        // Assert: one successful write (the in-threshold missing upsert) saved once; dry run and failed write did not.
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.SaveAsync(It.Is<D1SearchReconcileWatermark>(w => w.HighWaterMark == ts),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When --full is set, the reconcile scans the whole catalogue even if a watermark exists, because --full is " +
        "the safety compare and the weekly timer uses it.")]
    public async Task full_ignores_the_watermark_and_scans_everything()
    {
        // Arrange
        const long since = 1_700_000_000;
        _catalogue = [Candidate(modifiedAt: since * 1000)];
        _inventory = _catalogue.ToDictionary(c => c.Id, c => c.Document!.ModifiedAt);
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>()
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new D1SearchReconcileWatermark { HighWaterMark = since });
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true, Full: true), CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchReconcileOutcome.InSync);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.Read(0, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(
            c => c.ReadIncremental(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.SaveAsync(It.Is<D1SearchReconcileWatermark>(w => w.HighWaterMark == since),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A full catalogue pass reports a known total when CountPlayables is available, because the catalogue bar " +
        "must be determinate instead of counting up with no end.")]
    public async Task full_catalogue_progress_is_determinate_when_the_count_is_known()
    {
        // Arrange
        _catalogue = Enumerable.Range(0, 25).Select(_ => Candidate(modifiedAt: 1)).ToList();
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.CountPlayables(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);
        var progress = new CollectingProgress();
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None, progress);

        // Assert
        progress.Items.Should().Contain(new D1SearchReconcileProgress(
            D1SearchReconcilePhase.Catalogue, 0, 25, "waiting for first page"));
        progress.Items.Should().Contain(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, 1, 25));
        progress.Items.Should().Contain(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, 25, 25));
    }

    [Fact(DisplayName =
        "A document whose stored modifiedAt equals the playable's own _ts is in sync even when its podcast was saved " +
        "later, because a podcast _ts bump alone is not an indexed change (#1475: 58,187 false stale).")]
    public async Task podcast_only_bump_is_not_stale()
    {
        // Arrange
        const long episodeTs = 1_700_000_000;
        const long podcastTs = 1_700_500_000;
        var candidate = Candidate(modifiedAt: episodeTs * 1000) with { TimestampSeconds = podcastTs };
        _catalogue = [candidate];
        _inventory = new() { [candidate.Id] = episodeTs * 1000 };
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(false), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new { Outcome = D1SearchReconcileOutcome.InSync, Stale = 0, Drift = 0.0 });
    }

    [Fact(DisplayName =
        "An incremental episode marked ParentChanged is stale and re-sent even though its modifiedAt equals the " +
        "stored one, because its podcast changed an indexed field and the Worker applies an equal modifiedAt.")]
    public async Task parent_changed_episode_is_stale_and_upserted()
    {
        // Arrange
        const long since = 1_700_000_000;
        _options.Reconcile.DriftThreshold = 1;
        var changed = Candidate(modifiedAt: (since - 50) * 1000) with
        {
            TimestampSeconds = since + 5, ParentChanged = true
        };
        var unchanged = Candidate(modifiedAt: (since - 50) * 1000) with { TimestampSeconds = since + 5 };
        _catalogue = [changed, unchanged];
        _inventory = new() { [changed.Id] = (since - 50) * 1000, [unchanged.Id] = (since - 50) * 1000 };
        SetupIncremental(since, [changed.Id, unchanged.Id]);
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new
        {
            Outcome = D1SearchReconcileOutcome.Patched, Stale = 1, Upserted = 1, Scan = D1SearchReconcileScan.Incremental,
            SinceTs = since
        });
        _writes.Should().BeEquivalentTo(new[] { (new[] { changed.Id }, Array.Empty<string>()) });
    }

    [Fact(DisplayName =
        "With no watermark, the run says it is a full scan because there is no watermark yet, and a write that finds " +
        "nothing to change still saves the watermark, because the first successful --write must make later runs " +
        "incremental.")]
    public async Task first_run_is_labelled_and_an_in_sync_write_saves_the_watermark()
    {
        // Arrange
        const long ts = 1_700_000_000;
        var candidate = Candidate(modifiedAt: ts * 1000) with { TimestampSeconds = ts + 7 };
        _catalogue = [candidate];
        _inventory = new() { [candidate.Id] = ts * 1000 };
        var progress = new CollectingProgress();
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None, progress);

        // Assert
        report.Should().BeEquivalentTo(new
        {
            Outcome = D1SearchReconcileOutcome.InSync, Scan = D1SearchReconcileScan.FullNoWatermark, SinceTs = 0L
        });
        progress.Items.Should().Contain(
            new D1SearchReconcileProgress(D1SearchReconcilePhase.Starting, 1, 1, "full: no watermark yet"));
        _writes.Should().BeEmpty();
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>().Verify(
            s => s.SaveAsync(It.Is<D1SearchReconcileWatermark>(w => w.HighWaterMark == ts + 7),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName =
        "The scan description names the mode, and an incremental one gives the watermark as a UTC time, because the " +
        "console must say plainly whether the run is a full scan and why.")]
    [InlineData(D1SearchReconcileScan.Full, 0, "full scan (--full)")]
    [InlineData(D1SearchReconcileScan.FullNoWatermark, 0, "full scan: no watermark yet")]
    [InlineData(D1SearchReconcileScan.Incremental, 1_700_000_000, "incremental from _ts 1700000000 (2023-11-14 22:13:20 UTC)")]
    public void scan_description_names_the_mode(D1SearchReconcileScan scan, long sinceTs, string expected)
    {
        // Arrange
        var describe = D1SearchReconcileScanText.Describe;

        // Act
        var text = describe(scan, sinceTs);

        // Assert
        text.Should().StartWith(expected);
    }

    [Theory(DisplayName =
        "The bar's scan label is short, because the Starting task's description must not wrap the progress bar on a " +
        "normal-width terminal.")]
    [InlineData(D1SearchReconcileScan.Full, 0, "full (--full)")]
    [InlineData(D1SearchReconcileScan.FullNoWatermark, 0, "full: no watermark yet")]
    [InlineData(D1SearchReconcileScan.Incremental, 1_700_000_000, "incremental since 2023-11-14 22:13 UTC")]
    public void scan_label_is_short(D1SearchReconcileScan scan, long sinceTs, string expected)
    {
        // Arrange
        const int maxLabelLength = 40;

        // Act
        var label = D1SearchReconcileScanText.Label(scan, sinceTs);

        // Assert
        label.Should().Be(expected);
        label.Length.Should().BeLessThanOrEqualTo(maxLabelLength);
    }

    [Fact(DisplayName =
        "When the watermark can't be read, the run fails with no scan reported, because the console must not claim a " +
        "full or incremental scan that never ran.")]
    public async Task watermark_read_failure_reports_no_scan()
    {
        // Arrange
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>()
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<D1SearchReconciler>();

        // Act
        var report = await sut.Reconcile(new D1SearchReconcileRequest(true), CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchReconcileOutcome.Failed);
        report.Scan.Should().BeNull();
        _writes.Should().BeEmpty();
    }

    private void SetupIncremental(long since, IEnumerable<string> eligibleIds)
    {
        _mocker.GetMock<ID1SearchReconcileWatermarkStore>()
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new D1SearchReconcileWatermark { HighWaterMark = since });
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.ReadIncremental(since, It.IsAny<CancellationToken>()))
            .Returns(() => _catalogue.ToAsyncEnumerable());
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.ReadEligibleIds(It.IsAny<CancellationToken>()))
            .Returns(eligibleIds.ToAsyncEnumerable());
    }
    private sealed class CollectingProgress : IProgress<D1SearchReconcileProgress>
    {
        public List<D1SearchReconcileProgress> Items { get; } = [];
        public void Report(D1SearchReconcileProgress value) => Items.Add(value);
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
