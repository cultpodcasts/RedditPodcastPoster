using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EpisodeWireRewrite;

public sealed class WireCutoverRunResult
{
    public required int Scanned { get; init; }
    public required int NeedChange { get; init; }
    public required int Written { get; init; }
    public required int Failed { get; init; }
    public required int Unchanged { get; init; }
    public required bool Applied { get; init; }
    public required string Mode { get; init; }
    public int Limit { get; init; }
    public string? JournalPath { get; init; }
    public string? AffectedIdsPath { get; init; }
    public string? AppliedIdsPath { get; init; }
    public string? ChangesPath { get; init; }
    public IReadOnlyList<Guid> AffectedEpisodeIds { get; init; } = [];
}

public sealed class WireCutoverProcessor(
    IWireCutoverEpisodeStore store,
    IWireCutoverProgressReporter progressReporter,
    ILogger<WireCutoverProcessor> logger)
{
    /// <param name="limit">
    /// Max episodes that still need cutover. 0 = unlimited.
    /// Already-migrated documents log no-action and do not count.
    /// Stream must be in stable ORDER BY id order.
    /// </param>
    public async Task<WireCutoverRunResult> MigrateAsync(
        IAsyncEnumerable<string> documents,
        bool apply,
        string journalPath,
        int progressEvery,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be >= 0.");
        }

        using var journal = new WireCutoverJournal(journalPath);
        var tracker = new CountingProgress(progressReporter, progressEvery, apply, "migrate");
        var affected = new List<Guid>();
        var needChangeTaken = 0;

        await foreach (var json in documents.WithCancellation(cancellationToken))
        {
            if (limit > 0 && needChangeTaken >= limit)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                tracker.Unchanged();
                tracker.Scanned();
                continue;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var episodeId = TryPeekId(root);
            tracker.Scanned(episodeId);

            if (!WireCutoverPlanner.TryCreateChange(root, out var change) || change is null)
            {
                tracker.Unchanged();
                logger.LogInformation(
                    "Wire cutover: no action for episode {EpisodeId} (already migrated or no cutover fields).",
                    episodeId);
                continue;
            }

            needChangeTaken++;
            tracker.NeedChange();
            affected.Add(change.EpisodeId);

            if (!apply)
            {
                journal.Append(change, applied: false);
                continue;
            }

            var ok = await store.ApplyCutoverStateAsync(
                change.PodcastId,
                change.EpisodeId,
                change.After,
                cancellationToken);
            if (!ok)
            {
                tracker.Failed();
                journal.Append(change, applied: false);
                logger.LogError(
                    "Wire cutover migrate failed for episode {EpisodeId} podcast {PodcastId}.",
                    change.EpisodeId,
                    change.PodcastId);
                continue;
            }

            journal.Append(change, applied: true);
            tracker.Written(change.EpisodeId);
        }

        var snap = tracker.Complete(
            $"Migrate complete. limit={limit} Journal={journal.Path} AffectedIds={journal.AffectedIdsPath} AppliedIds={journal.AppliedIdsPath} Changes={journal.ChangesPath}");
        return new WireCutoverRunResult
        {
            Scanned = snap.Scanned,
            NeedChange = snap.NeedChange,
            Written = snap.Written,
            Failed = snap.Failed,
            Unchanged = snap.SkippedUnchanged,
            Applied = apply,
            Mode = "migrate",
            Limit = limit,
            JournalPath = journal.Path,
            AffectedIdsPath = journal.AffectedIdsPath,
            AppliedIdsPath = journal.AppliedIdsPath,
            ChangesPath = journal.ChangesPath,
            AffectedEpisodeIds = affected
        };
    }

    public async Task<WireCutoverRunResult> RollbackAsync(
        string journalPath,
        bool apply,
        int progressEvery,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        var entries = WireCutoverJournal.ReadAll(journalPath)
            .Where(e => e.Applied || !apply)
            .GroupBy(e => e.EpisodeId)
            .Select(g => g.Last())
            .ToList();

        if (apply)
        {
            entries = WireCutoverJournal.ReadAll(journalPath)
                .Where(e => e.Applied)
                .GroupBy(e => e.EpisodeId)
                .Select(g => g.Last())
                .ToList();
        }

        var tracker = new CountingProgress(progressReporter, progressEvery, apply, "rollback");
        var affected = new List<Guid>();
        var dop = Math.Max(1, maxDegreeOfParallelism);

        await Parallel.ForEachAsync(
            entries,
            new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = cancellationToken },
            async (entry, ct) =>
            {
                tracker.Scanned(entry.EpisodeId);
                tracker.NeedChange();
                lock (affected)
                {
                    affected.Add(entry.EpisodeId);
                }

                var target = WireCutoverPlanner.RollbackTarget(entry.ToChange());

                if (!apply)
                {
                    return;
                }

                var liveJson = await store.GetRawAsync(entry.PodcastId, entry.EpisodeId, ct);
                if (liveJson is null)
                {
                    tracker.Failed();
                    logger.LogError(
                        "Wire cutover rollback: episode {EpisodeId} podcast {PodcastId} missing.",
                        entry.EpisodeId,
                        entry.PodcastId);
                    return;
                }

                using var liveDoc = JsonDocument.Parse(liveJson);
                var current = WireCutoverPlanner.Capture(liveDoc.RootElement);
                var ops = WireCutoverPatchBuilder.BuildDelta(current, target);
                if (ops.Count == 0)
                {
                    tracker.Unchanged();
                    logger.LogInformation(
                        "Wire cutover rollback: no action for episode {EpisodeId} (already at Before state).",
                        entry.EpisodeId);
                    return;
                }

                var ok = await store.ApplyCutoverStateAsync(entry.PodcastId, entry.EpisodeId, target, ct);
                if (!ok)
                {
                    tracker.Failed();
                    return;
                }

                tracker.Written(entry.EpisodeId);
            });

        var snap = tracker.Complete(
            $"Rollback complete from journal {Path.GetFullPath(journalPath)}. apply={apply}");
        return new WireCutoverRunResult
        {
            Scanned = snap.Scanned,
            NeedChange = snap.NeedChange,
            Written = snap.Written,
            Failed = snap.Failed,
            Unchanged = snap.SkippedUnchanged,
            Applied = apply,
            Mode = "rollback",
            JournalPath = Path.GetFullPath(journalPath),
            AffectedEpisodeIds = affected
        };
    }

    private static Guid? TryPeekId(JsonElement root) =>
        WireCutoverPlanner.TryReadRef(root, out var r) ? r.EpisodeId : null;

    private sealed class CountingProgress(
        IWireCutoverProgressReporter reporter,
        int every,
        bool apply,
        string mode)
    {
        private int _scanned;
        private int _need;
        private int _written;
        private int _failed;
        private int _unchanged;
        private Guid? _last;
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        public void Scanned(Guid? id = null)
        {
            var n = Interlocked.Increment(ref _scanned);
            if (id.HasValue)
            {
                _last = id;
            }

            if (n % Math.Max(1, every) == 0)
            {
                reporter.Report(Snap());
            }
        }

        public void NeedChange() => Interlocked.Increment(ref _need);
        public void Written(Guid id)
        {
            Interlocked.Increment(ref _written);
            _last = id;
        }

        public void Failed() => Interlocked.Increment(ref _failed);
        public void Unchanged() => Interlocked.Increment(ref _unchanged);

        public WireCutoverProgressSnapshot Complete(string summary)
        {
            var snap = Snap();
            reporter.Completed(snap, summary);
            return snap;
        }

        private WireCutoverProgressSnapshot Snap() => new()
        {
            Scanned = Volatile.Read(ref _scanned),
            NeedChange = Volatile.Read(ref _need),
            Written = Volatile.Read(ref _written),
            Failed = Volatile.Read(ref _failed),
            SkippedUnchanged = Volatile.Read(ref _unchanged),
            LastEpisodeId = _last,
            Elapsed = _clock.Elapsed,
            Apply = apply,
            Mode = mode
        };
    }
}
