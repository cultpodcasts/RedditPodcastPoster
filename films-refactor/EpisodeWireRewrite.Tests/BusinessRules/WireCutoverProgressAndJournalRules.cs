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
        var utc = new DateTime(2026, 9, 24, 17, 30, 0, DateTimeKind.Utc);

        // Act
        var path = WireCutoverEvidencePaths.ResolveMigrateJournalPath(journalOverride: null, utc);

        // Assert
        path.Should().Contain(WireCutoverEvidencePaths.DefaultDirectoryName);
        path.Should().EndWith("wire-cutover-20260924-173000Z.jsonl");
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
        "because small live batches must be repeatable.")]
    public async Task limit_takes_first_n_needing_change_in_id_order()
    {
        // Arrange
        var store = new InMemoryWireCutoverStore();
        var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var idC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
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
            limit: 2);

        // Assert
        result.NeedChange.Should().Be(2);
        result.Written.Should().Be(2);
        result.Failed.Should().Be(0);
        result.AffectedEpisodeIds.Should().Equal(idA, idB);
        store.GetRaw(podcastId, idC).Should().Contain("podcastSearchTerms");
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
    public int ApplyCount { get; private set; }

    public void Add(string json)
    {
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryReadRef(doc.RootElement, out var r).Should().BeTrue();
        _docs[(r.PodcastId, r.EpisodeId)] = json;
    }

    public string? GetRaw(Guid podcastId, Guid episodeId) =>
        _docs.TryGetValue((podcastId, episodeId), out var json) ? json : null;

    public async IAsyncEnumerable<string> QueryAllRawAsync(CancellationToken cancellationToken = default)
    {
        foreach (var json in _docs.Values.OrderBy(j =>
                 {
                     using var d = JsonDocument.Parse(j);
                     return d.RootElement.TryGetProperty("id", out var id) ? id.GetString() : "";
                 }, StringComparer.Ordinal))
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
            var match = _docs.FirstOrDefault(kv => kv.Key.EpisodeId == id).Value;
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
        if (!_docs.TryGetValue((podcastId, episodeId), out var json))
        {
            return Task.FromResult(false);
        }

        ApplyCount++;
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, desired);
        _docs[(podcastId, episodeId)] = mut.GetRawText();
        return Task.FromResult(true);
    }
}
