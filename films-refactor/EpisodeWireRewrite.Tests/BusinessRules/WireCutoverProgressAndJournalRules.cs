using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace EpisodeWireRewrite.Tests.BusinessRules;

public class WireCutoverProgressAndJournalRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "PROGRESS: reporter receives scanned/needChange/written/last id lines, " +
        "because GATE 1 operators must see continuous progress during long runs.")]
    public async Task progress_reporter_receives_snapshots()
    {
        // Arrange
        var reports = new List<WireCutoverProgressSnapshot>();
        var reporter = new CollectingReporter(reports);
        var store = new InMemoryWireCutoverStore();
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.Create<string>());
        store.Add(json);
        var processor = new WireCutoverProcessor(
            store,
            reporter,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-{_fixture.CreateGuid():N}.jsonl");

        // Act
        var result = await processor.MigrateAsync(
            store.QueryAllRawAsync(),
            apply: true,
            journalPath,
            progressEvery: 1);

        // Assert
        reports.Should().NotBeEmpty();
        reports.Should().Contain(r => r.Scanned >= 1 && r.Mode == "migrate");
        result.AffectedEpisodeIds.Should().Contain(episodeId);
        File.Exists(journalPath).Should().BeTrue();
        var idsPath = Path.ChangeExtension(journalPath, null) + "-affected-ids.txt";
        File.Exists(idsPath).Should().BeTrue();
        (await File.ReadAllTextAsync(idsPath)).Should().Contain(episodeId.ToString());
        var changesPath = Path.ChangeExtension(journalPath, null) + "-changes.txt";
        File.Exists(changesPath).Should().BeTrue();
        var changesText = await File.ReadAllTextAsync(changesPath);
        changesText.Should().Contain(episodeId.ToString());
        changesText.Should().Contain("podcastSearchTerms:");
        changesText.Should().Contain("->");
        changesText.Should().Contain("publisherSearchTerms:");
        var appliedPath = Path.ChangeExtension(journalPath, null) + "-applied-ids.txt";
        File.Exists(appliedPath).Should().BeTrue();
        (await File.ReadAllTextAsync(appliedPath)).Should().Contain(episodeId.ToString());

        // Repair contract: applied journal row has Before sufficient to restore by hand / fixer app
        var entry = WireCutoverJournal.ReadAll(journalPath).Single(e => e.Applied);
        entry.SchemaVersion.Should().Be(WireCutoverJournal.SchemaVersion);
        entry.FieldChanges.Should().NotBeEmpty();
        entry.Before.Should().NotBeEmpty();
        entry.FieldChanges.Should().OnlyContain(c =>
            c.FromExists != c.ToExists || c.FromJson != c.ToJson);
    }

    [Fact(DisplayName =
        "CHANGE LOG: each FieldChange states from and to values for renamed/removed/added cutover fields, " +
        "because if rollback fails operators must repair Episodes from an explicit from→to record.")]
    public void field_changes_state_from_and_to_for_each_touched_property()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search);
        using var doc = JsonDocument.Parse(json);

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var podcastSearch = change!.FieldChanges.Single(c => c.Name == WireCutoverFields.PodcastSearchTerms);
        var publisherSearch = change.FieldChanges.Single(c => c.Name == WireCutoverFields.PublisherSearchTerms);

        // Assert
        podcastSearch.FromExists.Should().BeTrue();
        podcastSearch.FromJson.Should().Contain(search);
        podcastSearch.ToExists.Should().BeFalse();
        publisherSearch.FromExists.Should().BeFalse();
        publisherSearch.ToExists.Should().BeTrue();
        publisherSearch.ToJson.Should().Contain(search);
        podcastSearch.ToDisplayLine().Should().Contain("->");
    }

    [Fact(DisplayName =
        "JOURNAL: dry-run still writes collectable episode ids without applying patches, " +
        "because operators must know the blast radius before --apply.")]
    public async Task dry_run_journals_affected_ids_without_writes()
    {
        // Arrange
        var store = new InMemoryWireCutoverStore();
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        store.Add(WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: _fixture.Create<string>()));
        var processor = new WireCutoverProcessor(
            store,
            new CollectingReporter([]),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-dry-{_fixture.CreateGuid():N}.jsonl");

        // Act
        var result = await processor.MigrateAsync(
            store.QueryAllRawAsync(),
            apply: false,
            journalPath,
            progressEvery: 1);

        // Assert
        result.Written.Should().Be(0);
        result.NeedChange.Should().Be(1);
        store.ApplyCount.Should().Be(0);
        result.AffectedEpisodeIds.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "ROLLBACK PROCESSOR: apply rollback restores legacy JSON in the store from journal Before, " +
        "because production rollback must use the journal not a re-plan.")]
    public async Task rollback_processor_restores_from_journal()
    {
        // Arrange
        var store = new InMemoryWireCutoverStore();
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        store.Add(WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search));
        var processor = new WireCutoverProcessor(
            store,
            new CollectingReporter([]),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-rb-{_fixture.CreateGuid():N}.jsonl");
        await processor.MigrateAsync(store.QueryAllRawAsync(), apply: true, journalPath, 1);

        // Act
        var rollback = await processor.RollbackAsync(journalPath, apply: true, progressEvery: 1, maxDegreeOfParallelism: 1);
        using var doc = JsonDocument.Parse(store.GetRaw(podcastId, episodeId)!);

        // Assert
        rollback.Written.Should().Be(1);
        doc.RootElement.GetProperty("podcastSearchTerms").GetString().Should().Be(search);
        doc.RootElement.TryGetProperty("publisherSearchTerms", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "REPAIR EVIDENCE: journal Before plus FieldChanges from→to is enough to restore an Episode after failed rollback, " +
        "because operators must be able to fix by hand or write a fixer that reads the log.")]
    public void journal_before_and_field_changes_are_complete_repair_evidence()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search, removed: true);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);

        // Act — simulate fixer: restore Before from journal payload only
        WireCutoverPlanner.ApplyState(mut, change.Before);
        using var repaired = JsonDocument.Parse(mut.GetRawText());

        // Assert
        change.FieldChanges.Should().Contain(c =>
            c.Name == WireCutoverFields.PodcastSearchTerms && c.FromExists && !c.ToExists);
        change.FieldChanges.Should().Contain(c =>
            c.Name == WireCutoverFields.PublisherSearchTerms && !c.FromExists && c.ToExists);
        repaired.RootElement.GetProperty("podcastSearchTerms").GetString().Should().Be(search);
        repaired.RootElement.GetProperty("podcastRemoved").GetBoolean().Should().BeTrue();
        repaired.RootElement.TryGetProperty("publisherSearchTerms", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "SAFETY: migrate always resolves a concrete evidence journal path without --journal, " +
        "because the repair pack must never be opt-in.")]
    public void migrate_evidence_path_is_always_resolved_without_opt_in()
    {
        // Arrange
        var utc = DomainTestFixture.UtcAtTime(0, new TimeSpan(17, 30, 0));

        // Act
        var path = WireCutoverEvidencePaths.ResolveMigrateJournalPath(journalOverride: null, utc);

        // Assert
        path.Should().Contain(WireCutoverEvidencePaths.DefaultDirectoryName);
        path.Should().EndWith($"wire-cutover-{utc:yyyyMMdd-HHmmss}Z.jsonl");
        Path.IsPathRooted(path).Should().BeTrue();
    }

    [Fact(DisplayName =
        "SAFETY: rollback without --journal is refused with a clear error, " +
        "because reversing requires selecting an existing evidence pack (migrate always created one).")]
    public void rollback_without_journal_is_refused()
    {
        // Arrange / Act
        var ok = WireCutoverEvidencePaths.TryResolveRollbackJournalPath(
            null,
            out _,
            out var error);

        // Assert
        ok.Should().BeFalse();
        error.Should().Contain("--journal");
        error.Should().Contain("always written");
    }

    [Fact(DisplayName =
        "LIMIT: with --limit N only the first N episodes that still need cutover are worked (stable id order), " +
        "because small live batches must be repeatable even when apply uses --dop parallelism.")]
    public async Task limit_takes_first_n_needing_change_in_id_order()
    {
        // Arrange — generate ids then sort so ORDER BY id ASC expectation is explicit
        var store = new InMemoryWireCutoverStore();
        var orderedIds = Enumerable.Range(0, 3)
            .Select(_ => _fixture.CreateGuid())
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToList();
        var idA = orderedIds[0];
        var idB = orderedIds[1];
        var idC = orderedIds[2];
        var podcastId = _fixture.CreateGuid();
        store.Add(WireCutoverTestJson.LegacyDoc(idC, podcastId, searchTerms: _fixture.Create<string>()));
        store.Add(WireCutoverTestJson.LegacyDoc(idA, podcastId, searchTerms: _fixture.Create<string>()));
        store.Add(WireCutoverTestJson.LegacyDoc(idB, podcastId, searchTerms: _fixture.Create<string>()));
        var processor = new WireCutoverProcessor(
            store,
            new CollectingReporter([]),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-lim-{_fixture.CreateGuid():N}.jsonl");

        // Act
        var result = await processor.MigrateAsync(
            store.QueryAllRawAsync(),
            apply: true,
            journalPath,
            progressEvery: 1,
            limit: 2,
            maxDegreeOfParallelism: 4);

        // Assert
        result.NeedChange.Should().Be(2);
        result.Written.Should().Be(2);
        result.Failed.Should().Be(0);
        result.AffectedEpisodeIds.Should().Equal(idA, idB);
        store.GetRaw(podcastId, idC).Should().Contain("podcastSearchTerms");
    }

    [Fact(DisplayName =
        "MIGRATE APPLY: with --dop greater than 1, Cosmos patches overlap through a bounded channel while " +
        "scan/plan stays sequential, because apply is one-by-one in parallel without buffering the corpus.")]
    public async Task migrate_apply_uses_bounded_parallel_pipeline()
    {
        // Arrange
        var store = new ConcurrentDelayWireCutoverStore(TimeSpan.FromMilliseconds(80));
        var podcastId = _fixture.CreateGuid();
        for (var i = 0; i < 6; i++)
        {
            var episodeId = _fixture.CreateGuid();
            store.Add(WireCutoverTestJson.LegacyDoc(
                episodeId,
                podcastId,
                searchTerms: _fixture.Create<string>()));
        }

        var processor = new WireCutoverProcessor(
            store,
            new CollectingReporter([]),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-dop-{_fixture.CreateGuid():N}.jsonl");

        // Act
        var result = await processor.MigrateAsync(
            store.QueryAllRawAsync(),
            apply: true,
            journalPath,
            progressEvery: 1,
            limit: 0,
            maxDegreeOfParallelism: 4);

        // Assert
        result.NeedChange.Should().Be(6);
        result.Written.Should().Be(6);
        result.Failed.Should().Be(0);
        store.MaxConcurrentApplies.Should().BeGreaterThan(1);
        store.ApplyCount.Should().Be(6);
    }

    [Fact(DisplayName =
        "NO-ACTION: already-migrated episodes are skipped with success (not failure), " +
        "because re-running a limited apply must not abort on docs already cut over.")]
    public async Task already_migrated_is_no_action_not_failure()
    {
        // Arrange
        var store = new InMemoryWireCutoverStore();
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        var legacy = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search);
        using var legDoc = JsonDocument.Parse(legacy);
        WireCutoverPlanner.TryCreateChange(legDoc.RootElement, out var plan).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(legacy);
        WireCutoverPlanner.ApplyState(mut, plan!.After);
        store.Add(mut.GetRawText());
        var processor = new WireCutoverProcessor(
            store,
            new CollectingReporter([]),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WireCutoverProcessor>.Instance);
        var journalPath = Path.Combine(Path.GetTempPath(), $"wire-noop-{_fixture.CreateGuid():N}.jsonl");

        // Act
        var result = await processor.MigrateAsync(
            store.QueryAllRawAsync(),
            apply: true,
            journalPath,
            progressEvery: 1,
            limit: 5);

        // Assert
        result.Failed.Should().Be(0);
        result.Written.Should().Be(0);
        result.Unchanged.Should().Be(1);
        result.NeedChange.Should().Be(0);
    }

    [Fact(DisplayName =
        "ORDER: container query text is ORDER BY c.id ASC, because limited runs must pick the same episodes every time.")]
    public void query_order_is_stable_by_id()
    {
        // Arrange / Act / Assert
        IWireCutoverEpisodeStore.OrderedAllQuery.Should().Contain("ORDER BY c.id");
    }

    private sealed class CollectingReporter(List<WireCutoverProgressSnapshot> sink) : IWireCutoverProgressReporter
    {
        public void Report(WireCutoverProgressSnapshot snapshot) => sink.Add(snapshot);
        public void Completed(WireCutoverProgressSnapshot snapshot, string summary) => sink.Add(snapshot);
    }
}

internal sealed class InMemoryWireCutoverStore : IWireCutoverEpisodeStore
{
    private readonly Dictionary<(Guid PodcastId, Guid EpisodeId), string> _docs = new();
    private readonly object _gate = new();
    private int _applyCount;

    public int ApplyCount => Volatile.Read(ref _applyCount);

    internal void Add(string json)
    {
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryReadRef(doc.RootElement, out var r).Should().BeTrue();
        lock (_gate)
        {
            _docs[(r.PodcastId, r.EpisodeId)] = json;
        }
    }

    public string? GetRaw(Guid podcastId, Guid episodeId)
    {
        lock (_gate)
        {
            return _docs.TryGetValue((podcastId, episodeId), out var json) ? json : null;
        }
    }

    public async IAsyncEnumerable<string> QueryAllRawAsync(CancellationToken cancellationToken = default)
    {
        List<string> ordered;
        lock (_gate)
        {
            ordered = _docs.Values.OrderBy(j =>
            {
                using var d = JsonDocument.Parse(j);
                return d.RootElement.TryGetProperty("id", out var id) ? id.GetString() : "";
            }, StringComparer.Ordinal).ToList();
        }

        foreach (var json in ordered)
        {
            yield return json;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> QueryByEpisodeIdsAsync(
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var id in episodeIds.OrderBy(x => x))
        {
            string? match;
            lock (_gate)
            {
                match = _docs.FirstOrDefault(kv => kv.Key.EpisodeId == id).Value;
            }

            if (match is not null)
            {
                yield return match;
            }
        }

        await Task.CompletedTask;
    }

    public Task<string?> GetRawAsync(Guid podcastId, Guid episodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetRaw(podcastId, episodeId));

    public Task<bool> ApplyCutoverStateAsync(
        Guid podcastId,
        Guid episodeId,
        IReadOnlyList<WireFieldState> desired,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_docs.TryGetValue((podcastId, episodeId), out var json))
            {
                return Task.FromResult(false);
            }

            Interlocked.Increment(ref _applyCount);
            var mut = JsonObjectMutator.Parse(json);
            WireCutoverPlanner.ApplyState(mut, desired);
            _docs[(podcastId, episodeId)] = mut.GetRawText();
            return Task.FromResult(true);
        }
    }
}

/// <summary>
/// Delays each apply so overlapping Parallel.ForEachAsync work is observable.
/// </summary>
internal sealed class ConcurrentDelayWireCutoverStore(TimeSpan applyDelay) : IWireCutoverEpisodeStore
{
    private readonly InMemoryWireCutoverStore _inner = new();
    private int _current;
    private int _maxConcurrent;

    public int ApplyCount => _inner.ApplyCount;
    public int MaxConcurrentApplies => Volatile.Read(ref _maxConcurrent);

    internal void Add(string json) => _inner.Add(json);

    public async IAsyncEnumerable<string> QueryAllRawAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var json in _inner.QueryAllRawAsync(cancellationToken))
        {
            yield return json;
        }
    }

    public async IAsyncEnumerable<string> QueryByEpisodeIdsAsync(
        IReadOnlyList<Guid> episodeIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var json in _inner.QueryByEpisodeIdsAsync(episodeIds, cancellationToken))
        {
            yield return json;
        }
    }

    public Task<string?> GetRawAsync(Guid podcastId, Guid episodeId, CancellationToken cancellationToken = default) =>
        _inner.GetRawAsync(podcastId, episodeId, cancellationToken);

    public async Task<bool> ApplyCutoverStateAsync(
        Guid podcastId,
        Guid episodeId,
        IReadOnlyList<WireFieldState> desired,
        CancellationToken cancellationToken = default)
    {
        var now = Interlocked.Increment(ref _current);
        int snapshot;
        do
        {
            snapshot = Volatile.Read(ref _maxConcurrent);
            if (now <= snapshot)
            {
                break;
            }
        } while (Interlocked.CompareExchange(ref _maxConcurrent, now, snapshot) != snapshot);

        try
        {
            await Task.Delay(applyDelay, cancellationToken);
            return await _inner.ApplyCutoverStateAsync(podcastId, episodeId, desired, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _current);
        }
    }
}
