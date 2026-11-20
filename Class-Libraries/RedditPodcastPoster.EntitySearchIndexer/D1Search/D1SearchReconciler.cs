using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Search;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <param name="Write">Patch the index. Without it this is a dry run (report only).</param>
/// <param name="AllowAboveDriftThreshold">
/// Patch even when the drift is above the threshold: the initial backfill (D1 empty, 100% drift) or a deliberate
/// repair. Only the console sets it, and only when asked to.
/// </param>
/// <param name="Full">
/// Compare the whole catalogue with D1, ignoring the last-reconciled watermark. The weekly Functions timer always
/// does this. The console default is incremental once a watermark has been saved.
/// </param>
public sealed record D1SearchReconcileRequest(
    bool Write,
    bool AllowAboveDriftThreshold = false,
    bool Full = false)
{
    /// <summary>
    /// The <c>D1SearchReconcile</c> console's command line as a request. Returns null with <paramref name="error"/>
    /// set for an invalid combination.
    /// </summary>
    public static D1SearchReconcileRequest? FromCommandLine(bool write, bool allowDrift, out string? error) =>
        FromCommandLine(write, allowDrift, false, out error);

    public static D1SearchReconcileRequest? FromCommandLine(
        bool write, bool allowDrift, bool full, out string? error)
    {
        error = !write && allowDrift ? "--allow-drift only applies with --write." : null;
        return error is not null ? null : new D1SearchReconcileRequest(write, allowDrift, full);
    }

    /// <summary>0 when the index is in sync, patched or reported; 1 when it was not configured, failed or was refused.</summary>
    public static int ExitCode(D1SearchReconcileReport report) => report.Succeeded ? 0 : 1;
}

public enum D1SearchReconcileOutcome
{
    /// <summary>The sync is not enabled and configured (<c>d1Search</c>); nothing was read or written.</summary>
    NotConfigured,

    /// <summary>Report only: the request did not ask to write.</summary>
    DryRun,

    /// <summary>Nothing to do: D1 matches Cosmos.</summary>
    InSync,

    /// <summary>Missing and stale documents were upserted and orphans deleted.</summary>
    Patched,

    /// <summary>Drift above the threshold; nothing written (SP-2.3 rebuild, or a deliberate --allow-drift backfill).</summary>
    RefusedDrift,

    /// <summary>The inventory, the Cosmos read, or a write failed; see the log.</summary>
    Failed
}

/// <summary>How the catalogue was read for a reconcile.</summary>
public enum D1SearchReconcileScan
{
    /// <summary>The whole catalogue, because <c>--full</c> was asked for (and always on the weekly timer).</summary>
    Full,

    /// <summary>
    /// The whole catalogue, because no watermark has been saved yet. The first successful <c>--write</c> (patched or
    /// already in sync) saves one, and later runs are incremental.
    /// </summary>
    FullNoWatermark,

    /// <summary>Only what changed since the saved watermark.</summary>
    Incremental
}

public static class D1SearchReconcileScanText
{
    /// <summary>A short label for the console's Starting task, short enough not to wrap the progress bar.</summary>
    public static string Label(D1SearchReconcileScan scan, long sinceTs) => scan switch
    {
        D1SearchReconcileScan.Incremental =>
            $"incremental since {DateTimeOffset.FromUnixTimeSeconds(sinceTs).UtcDateTime:yyyy-MM-dd HH:mm} UTC",
        D1SearchReconcileScan.FullNoWatermark => "full: no watermark yet",
        _ => "full (--full)"
    };

    /// <summary>A one-line, human description of the scan, for the console and the log.</summary>
    public static string Describe(D1SearchReconcileScan scan, long sinceTs) => scan switch
    {
        D1SearchReconcileScan.Incremental =>
            $"incremental from _ts {sinceTs} ({DateTimeOffset.FromUnixTimeSeconds(sinceTs).UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC)",
        D1SearchReconcileScan.FullNoWatermark =>
            "full scan: no watermark yet (the first successful --write saves one; later runs are incremental)",
        _ => "full scan (--full)"
    };
}

/// <param name="Missing">Eligible in Cosmos, absent from D1.</param>
/// <param name="Stale">
/// In D1 with an older <c>modifiedAt</c> than the playable's own Cosmos <c>_ts</c>, or (incremental only) an episode
/// whose podcast changed an indexed field the episode's projection does not have yet
/// (<see cref="D1SearchEpisodeParentProjection"/>). A podcast saved without such a change does not make its episodes
/// stale (#1475).
/// </param>
/// <param name="Orphans">In D1 but not eligible in Cosmos (removed, ineligible, hard-deleted, or podcast gone).</param>
/// <param name="Drift">(Missing + Stale + Orphans) / max(D1 count, eligible count).</param>
/// <param name="Scan">
/// How the catalogue was read; null when the run stopped before that was decided (not configured, or the watermark
/// could not be read).
/// </param>
/// <param name="SinceTs">The watermark <c>_ts</c> an incremental scan read from; 0 for a full scan.</param>
public sealed record D1SearchReconcileReport(
    D1SearchReconcileOutcome Outcome,
    int D1Documents,
    int Eligible,
    int Missing,
    int Stale,
    int Orphans,
    int FailedToBuild,
    double Drift,
    int Upserted = 0,
    int Deleted = 0,
    string? Error = null,
    D1SearchReconcileScan? Scan = null,
    long SinceTs = 0)
{
    public bool Succeeded => Outcome is not (D1SearchReconcileOutcome.NotConfigured or D1SearchReconcileOutcome.Failed
        or D1SearchReconcileOutcome.RefusedDrift);
}

public interface ID1SearchReconciler
{
    /// <summary>
    /// Compares the D1 search index's inventory with the Cosmos catalogue and, when asked to write and within the drift
    /// threshold, upserts missing or stale documents and deletes orphans. Never throws for a D1, Cosmos or network
    /// failure; the report says <see cref="D1SearchReconcileOutcome.Failed"/>.
    /// <paramref name="progress"/> is optional and for the console only; the Functions timer omits it.
    /// </summary>
    Task<D1SearchReconcileReport> Reconcile(
        D1SearchReconcileRequest request,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress = null);
}

/// <summary>
/// The SP-2.2 (#1322) reconcile of the one D1 search index (production in the Functions; whatever <c>d1Search</c>
/// points at for the console). It catches what the push mirror and the hourly drip can't: hard-deleted Cosmos
/// documents (no <c>_ts</c> to see), writes lost in a long outage, and drift of any kind. It's also the initial
/// backfill (an empty D1 is 100% "missing").
/// <list type="number">
/// <item>A full compare (<c>--full</c>, the weekly timer, or the first run with no watermark) streams the catalogue
/// from <c>_ts</c> 0, after a cheap <c>Count()</c> so the catalogue bar has a total, and keeps
/// <c>id → modifiedAt</c> of the eligible playables (about 90k longs) and the ids whose document failed to build
/// (neither written nor deleted).</item>
/// <item>An incremental compare reads the last successful write's LookUps watermark and streams playables with
/// <c>_ts &gt;=</c> that value, plus every episode of a podcast whose <c>_ts</c> is in the same window (a podcast
/// rename or subject change must re-emit its episodes). The boundary second is re-read, because a document saved
/// later in that same second would otherwise be missed. Orphans are found with a cheap eligible-id projection
/// against the D1 inventory, not a second document build of the whole catalogue.</item>
/// <item>The whole D1 inventory (<c>{ id, modifiedAt }</c>) is diffed against that: missing, stale, orphans. If the
/// inventory can't be read the reconcile fails; a partial inventory would turn live documents into orphans.</item>
/// <item>A dry run, an in-sync index or drift above the threshold stops there, and does not advance the watermark.</item>
/// <item>On a full write, the catalogue is streamed again and the planned documents are sent, 100 per request; then
/// the orphans are deleted, 1000 per request. An incremental write sends the documents already built in pass 1
/// (the window is small). A failed write stops the run and leaves the watermark where it was. Documents are never
/// all held in memory on a full pass. A playable that changed in between is still right: the Worker's
/// <c>modifiedAt</c> guard keeps the newest, and the drip covers the rest.</item>
/// <item>An incremental compare also looks up, by id, every eligible id that is absent from D1 but outside the
/// window, so <c>missing</c> covers the whole catalogue and those documents are sent (#1479). Then
/// <c>eligible = D1 - orphans + missing</c> holds for both scans (build failures aside).</item>
/// <item>The watermark advances only after a successful <c>--write</c> run (patched or already in sync), to the
/// highest Cosmos <c>_ts</c> considered, but never past the moment the catalogue read started (less
/// <see cref="WatermarkSafetySeconds"/>). A playable saved during the read, in a part already streamed, would
/// otherwise sit below a watermark set by a later save and never be read again (#1479).</item>
/// </list>
/// </summary>
public sealed class D1SearchReconciler(
    ID1SearchCatalogue catalogue,
    ID1SearchInventoryReader inventoryReader,
    ID1SearchIndexWriter writer,
    ID1SearchReconcileWatermarkStore watermarkStore,
    IOptions<D1SearchOptions> options,
    TimeProvider timeProvider,
    ILogger<D1SearchReconciler> logger) : ID1SearchReconciler
{
    /// <summary>
    /// The watermark stays this far before the start of the catalogue read, so Cosmos <c>_ts</c> (server clock) and
    /// this machine's clock may disagree by up to this much without a playable being skipped. Re-reading a few
    /// minutes' changes is cheap (#1479).
    /// </summary>
    public const long WatermarkSafetySeconds = 300;

    /// <summary>
    /// After the first candidate, report every this many catalogue items so the bar moves during
    /// the first Cosmos page and per-podcast lookups, not only every 100 documents.
    /// </summary>
    public const int CatalogueProgressInterval = 10;

    private readonly D1SearchReconcileOptions _options = options.Value.Reconcile;

    public async Task<D1SearchReconcileReport> Reconcile(
        D1SearchReconcileRequest request,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!writer.IsActive)
        {
            return Log(new D1SearchReconcileReport(D1SearchReconcileOutcome.NotConfigured, 0, 0, 0, 0, 0, 0, 0));
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Starting, 0, 1, "reading watermark"));

        D1SearchReconcileWatermark? watermark;
        try
        {
            watermark = request.Full ? null : await watermarkStore.GetAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read the watermark; nothing was written. {Message}", ex.Message);
            return Log(Failed(ex.Message));
        }

        var incremental = !request.Full && watermark?.HighWaterMark is > 0;
        var sinceTs = incremental ? watermark!.HighWaterMark!.Value : 0L;
        var scan = incremental
            ? D1SearchReconcileScan.Incremental
            : request.Full ? D1SearchReconcileScan.Full : D1SearchReconcileScan.FullNoWatermark;
        progress?.Report(new D1SearchReconcileProgress(
            D1SearchReconcilePhase.Starting, 1, 1, D1SearchReconcileScanText.Label(scan, sinceTs)));
        logger.LogInformation("D1 search reconcile: {Scan}.", D1SearchReconcileScanText.Describe(scan, sinceTs));

        D1SearchReconcileReport Done(D1SearchReconcileReport r) => Log(r with { Scan = scan, SinceTs = sinceTs });

        // #1479: nothing saved from here on can be skipped by the watermark this run saves.
        var readStartedSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds() - WatermarkSafetySeconds;
        CatalogueSnapshot snapshot;
        try
        {
            snapshot = await ReadCatalogue(sinceTs, incremental, cancellationToken, progress);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read Cosmos; nothing was written. {Message}", ex.Message);
            return Done(Failed(ex.Message));
        }

        IReadOnlyDictionary<string, long> inventory;
        try
        {
            var inventoryStarted = Stopwatch.StartNew();
            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Inventory, 0));
            inventory = await inventoryReader.ReadInventory(cancellationToken, progress);
            progress?.Report(new D1SearchReconcileProgress(
                D1SearchReconcilePhase.Inventory, inventory.Count, inventory.Count));
            logger.LogInformation(
                "D1 search reconcile inventory: {Count} document(s) in {Elapsed}.",
                inventory.Count, inventoryStarted.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read the D1 inventory; nothing was written. {Message}", ex.Message);
            return Done(Failed(ex.Message, snapshot.EligibleCount, snapshot.BuildFailed.Count));
        }

        if (incremental)
        {
            try
            {
                snapshot = await AddMissingOutsideWindow(snapshot, inventory, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex,
                    "D1 search reconcile could not read the eligible playables missing outside the window; nothing " +
                    "was written. {Message}", ex.Message);
                return Done(Failed(ex.Message, snapshot.EligibleCount, snapshot.BuildFailed.Count));
            }
        }

        var watermarkSeconds = Math.Min(snapshot.MaxTimestampSeconds, readStartedSeconds);
        var planStarted = Stopwatch.StartNew();
        var plan = Plan(snapshot, inventory);
        logger.LogInformation(
            "D1 search reconcile plan: {Elapsed} (eligible {Eligible}, D1 {D1Documents}, {Mode} from _ts {SinceTs}).",
            planStarted.Elapsed, snapshot.EligibleCount, inventory.Count,
            incremental ? "incremental" : "full", sinceTs);
        var report = plan.Report;
        if (plan.Upserts.Count + plan.Orphans.Count == 0)
        {
            var inSync = report with { Outcome = D1SearchReconcileOutcome.InSync };
            await TryAdvanceWatermark(request, inSync, watermarkSeconds, cancellationToken);
            return Done(inSync);
        }

        if (!request.Write)
        {
            return Done(report);
        }

        if (report.Drift > _options.DriftThreshold && !request.AllowAboveDriftThreshold)
        {
            return Done(report with { Outcome = D1SearchReconcileOutcome.RefusedDrift });
        }

        var (upserted, deleted, error) = await Apply(plan, snapshot, sinceTs, incremental, cancellationToken, progress);
        var written = report with
        {
            Outcome = error is null ? D1SearchReconcileOutcome.Patched : D1SearchReconcileOutcome.Failed,
            Upserted = upserted,
            Deleted = deleted,
            Error = error
        };
        await TryAdvanceWatermark(request, written, watermarkSeconds, cancellationToken);
        return Done(written);
    }

    private async Task<CatalogueSnapshot> ReadCatalogue(
        long sinceTs,
        bool incremental,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress)
    {
        var eligible = new Dictionary<string, long>(StringComparer.Ordinal);
        var buildFailed = new HashSet<string>(StringComparer.Ordinal);
        var parentChanged = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, D1SearchDocument>? held = incremental
            ? new Dictionary<string, D1SearchDocument>(StringComparer.Ordinal)
            : null;
        var seen = 0;
        var maxTimestampSeconds = 0L;
        var started = Stopwatch.StartNew();

        int? total = null;
        if (progress is not null && !incremental)
        {
            var count = await catalogue.CountPlayables(cancellationToken);
            if (count > 0)
            {
                total = count;
            }
        }

        progress?.Report(new D1SearchReconcileProgress(
            D1SearchReconcilePhase.Catalogue, 0, total, "waiting for first page"));

        var stream = incremental
            ? catalogue.ReadIncremental(sinceTs, cancellationToken)
            : catalogue.Read(sinceTs, cancellationToken);
        await foreach (var candidate in stream.WithCancellation(cancellationToken))
        {
            maxTimestampSeconds = Math.Max(maxTimestampSeconds, TimestampSeconds(candidate));
            if (candidate.BuildFailed)
            {
                buildFailed.Add(candidate.Id);
            }
            else if (candidate.Document is { } document)
            {
                // A kind change can bring an id twice; the first document wins, as pass 2 sends the first too.
                if (eligible.TryAdd(candidate.Id, document.ModifiedAt))
                {
                    held?.TryAdd(candidate.Id, document);
                    if (candidate.ParentChanged)
                    {
                        parentChanged.Add(candidate.Id);
                    }
                }
            }

            seen++;
            if (seen == 1)
            {
                logger.LogInformation(
                    "D1 search reconcile catalogue: first candidate after {Elapsed}.", started.Elapsed);
            }

            if (seen == 1 || seen % CatalogueProgressInterval == 0)
            {
                progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, seen, total));
            }
        }

        IReadOnlySet<string>? allEligibleIds = null;
        if (incremental)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var id in catalogue.ReadEligibleIds(cancellationToken).WithCancellation(cancellationToken))
            {
                ids.Add(id);
            }

            allEligibleIds = ids;
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Catalogue, seen, total ?? seen));
        logger.LogInformation(
            "D1 search reconcile catalogue: {Count} candidate(s) in {Elapsed} ({Mode} from _ts {SinceTs}, max _ts {MaxTs}).",
            seen, started.Elapsed, incremental ? "incremental" : "full", sinceTs, maxTimestampSeconds);
        return new CatalogueSnapshot(eligible, buildFailed, allEligibleIds, maxTimestampSeconds, held, parentChanged);
    }

    /// <summary>
    /// #1479: an incremental read only builds the <c>_ts</c> window, but its eligible count is the whole catalogue's
    /// id projection. An eligible id outside the window that D1 lacks (lost push, or a save the old watermark skipped)
    /// was neither counted as missing nor sent, so <c>eligible</c> exceeded <c>D1</c> while <c>missing</c> said 0.
    /// Those ids are read by id here, with the same rules as a full read: a buildable one joins the window as
    /// missing (and is sent on a write); one that is now excluded or gone leaves the eligible set, because the full
    /// rules are the ones the index follows; a failed build is kept, as in a full scan.
    /// </summary>
    private async Task<CatalogueSnapshot> AddMissingOutsideWindow(
        CatalogueSnapshot snapshot,
        IReadOnlyDictionary<string, long> inventory,
        CancellationToken cancellationToken)
    {
        var outside = snapshot.AllEligibleIds!
            .Where(id => !snapshot.Eligible.ContainsKey(id)
                         && !snapshot.BuildFailed.Contains(id)
                         && !inventory.ContainsKey(id))
            .ToHashSet(StringComparer.Ordinal);
        if (outside.Count == 0)
        {
            return snapshot;
        }

        var eligible = new Dictionary<string, long>(snapshot.Eligible, StringComparer.Ordinal);
        var held = new Dictionary<string, D1SearchDocument>(snapshot.HeldDocuments!, StringComparer.Ordinal);
        var buildFailed = new HashSet<string>(snapshot.BuildFailed, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var candidate in catalogue.ReadByIds(outside, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            if (!outside.Contains(candidate.Id))
            {
                continue;
            }

            if (candidate.BuildFailed)
            {
                buildFailed.Add(candidate.Id);
                found.Add(candidate.Id);
            }
            else if (candidate.Document is { } document)
            {
                // A kind change can bring an id twice; the document wins, as in a full read.
                eligible.TryAdd(candidate.Id, document.ModifiedAt);
                held.TryAdd(candidate.Id, document);
                found.Add(candidate.Id);
            }
        }

        var notEligible = outside.Where(id => !found.Contains(id)).ToList();
        var allEligibleIds = new HashSet<string>(snapshot.AllEligibleIds!, StringComparer.Ordinal);
        allEligibleIds.ExceptWith(notEligible);
        logger.LogInformation(
            "D1 search reconcile: {Outside} eligible id(s) missing from D1 outside the window; {Found} read by id " +
            "({Excluded} now excluded or gone).",
            outside.Count, found.Count, notEligible.Count);
        if (notEligible.Count > 0)
        {
            logger.LogWarning(
                "D1 search reconcile: the eligible-id projection and the full eligibility rules disagree on {Count} " +
                "id(s), e.g. {Sample}; they are not counted as eligible.",
                notEligible.Count, string.Join(", ", notEligible.Take(5)));
        }

        return snapshot with
        {
            Eligible = eligible,
            HeldDocuments = held,
            BuildFailed = buildFailed,
            AllEligibleIds = allEligibleIds
        };
    }

    private static long TimestampSeconds(D1SearchCandidate candidate)
    {
        if (candidate.TimestampSeconds > 0)
        {
            return candidate.TimestampSeconds;
        }

        return candidate.Document is { ModifiedAt: > 0 } document ? document.ModifiedAt / 1000 : 0;
    }

    private static ReconcilePlan Plan(CatalogueSnapshot snapshot, IReadOnlyDictionary<string, long> inventory)
    {
        int missing = 0, stale = 0;
        var upserts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, modifiedAt) in snapshot.Eligible)
        {
            if (!inventory.TryGetValue(id, out var stored))
            {
                missing++;
                upserts.Add(id);
            }
            else if (stored < modifiedAt || snapshot.ParentChanged.Contains(id))
            {
                stale++;
                upserts.Add(id);
            }
        }

        // #1479: a window document is eligible even if it became so after the id projection, so every id counted
        // missing is also counted eligible.
        var eligibleIds = snapshot.AllEligibleIds is { } all
            ? all.Union(snapshot.Eligible.Keys).ToHashSet(StringComparer.Ordinal)
            : snapshot.Eligible.Keys.ToHashSet(StringComparer.Ordinal);
        var orphans = inventory.Keys
            .Where(id => !eligibleIds.Contains(id) && !snapshot.BuildFailed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var eligibleCount = eligibleIds.Count;
        var drift = (double)(missing + stale + orphans.Length) / Math.Max(1, Math.Max(inventory.Count, eligibleCount));
        var report = new D1SearchReconcileReport(D1SearchReconcileOutcome.DryRun, inventory.Count,
            eligibleCount, missing, stale, orphans.Length, snapshot.BuildFailed.Count, drift);
        return new ReconcilePlan(report, upserts, orphans);
    }

    private async Task<(int Upserted, int Deleted, string? Error)> Apply(
        ReconcilePlan plan,
        CatalogueSnapshot snapshot,
        long sinceTs,
        bool incremental,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress)
    {
        int upserted = 0, deleted = 0;
        var pending = plan.Upserts;
        var upsertTotal = pending.Count;
        var batch = new List<D1SearchDocument>(D1SearchIndexWriter.MaxUpsertsPerRequest);
        try
        {
            if (pending.Count > 0 && incremental && snapshot.HeldDocuments is not null)
            {
                progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Upsert, 0, upsertTotal));
                foreach (var id in pending.ToArray())
                {
                    if (!snapshot.HeldDocuments.TryGetValue(id, out var document))
                    {
                        continue;
                    }

                    pending.Remove(id);
                    batch.Add(document);
                    if (batch.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest)
                    {
                        var error = await Flush();
                        if (error is not null)
                        {
                            return (upserted, deleted, error);
                        }
                    }
                }

                var heldFlush = await Flush();
                if (heldFlush is not null)
                {
                    return (upserted, deleted, heldFlush);
                }
            }
            else if (pending.Count > 0)
            {
                var scanned = 0;
                progress?.Report(new D1SearchReconcileProgress(
                    D1SearchReconcilePhase.Upsert, 0, upsertTotal, "waiting for first page"));
                await foreach (var candidate in catalogue.Read(sinceTs, cancellationToken)
                                   .WithCancellation(cancellationToken))
                {
                    scanned++;
                    if (scanned == 1 || scanned % CatalogueProgressInterval == 0)
                    {
                        progress?.Report(new D1SearchReconcileProgress(
                            D1SearchReconcilePhase.Upsert, upserted, upsertTotal, $"scanned {scanned:N0}"));
                    }

                    if (candidate.Document is not { } document || !pending.Remove(candidate.Id))
                    {
                        continue;
                    }

                    batch.Add(document);
                    if (batch.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest)
                    {
                        var error = await Flush();
                        if (error is not null)
                        {
                            return (upserted, deleted, error);
                        }
                    }

                    if (pending.Count == 0)
                    {
                        break;
                    }
                }

                var last = await Flush();
                if (last is not null)
                {
                    return (upserted, deleted, last);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile stopped reading Cosmos while writing. {Message}", ex.Message);
            return (upserted, deleted, ex.Message);
        }

        if (plan.Orphans.Count > 0)
        {
            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Delete, 0, plan.Orphans.Count));
        }

        foreach (var chunk in plan.Orphans.Chunk(D1SearchIndexWriter.MaxDeletesPerRequest))
        {
            var result = await writer.Write([], chunk, cancellationToken);
            deleted += result.Deleted;
            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Delete, deleted, plan.Orphans.Count));
            if (!result.Succeeded)
            {
                return (upserted, deleted, result.Error ?? "the delete failed");
            }
        }

        return (upserted, deleted, null);

        async Task<string?> Flush()
        {
            if (batch.Count == 0)
            {
                return null;
            }

            var result = await writer.Write(batch.ToArray(), [], cancellationToken);
            batch.Clear();
            upserted += result.Upserted;
            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Upsert, upserted, upsertTotal));
            return result.Succeeded ? null : result.Error ?? "the write failed";
        }
    }

    private async Task TryAdvanceWatermark(
        D1SearchReconcileRequest request,
        D1SearchReconcileReport report,
        long maxTimestampSeconds,
        CancellationToken cancellationToken)
    {
        if (!request.Write
            || report.Outcome is not (D1SearchReconcileOutcome.Patched or D1SearchReconcileOutcome.InSync)
            || maxTimestampSeconds <= 0)
        {
            return;
        }

        try
        {
            var watermark = new D1SearchReconcileWatermark
            {
                HighWaterMark = maxTimestampSeconds,
                UpdatedUtc = DateTime.UtcNow
            };
            await watermarkStore.SaveAsync(watermark, cancellationToken);
            logger.LogInformation(
                "D1 search reconcile watermark advanced to _ts {HighWaterMark}.", maxTimestampSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "D1 search reconcile wrote the index but could not save the watermark at _ts {HighWaterMark}. " +
                "The next incremental run will re-read this window. {Message}",
                maxTimestampSeconds, ex.Message);
        }
    }

    private static D1SearchReconcileReport Failed(string error, int eligible = 0, int failedToBuild = 0) =>
        new(D1SearchReconcileOutcome.Failed, 0, eligible, 0, 0, 0, failedToBuild, 0, Error: error);

    private D1SearchReconcileReport Log(D1SearchReconcileReport r)
    {
        var level = r.Outcome switch
        {
            D1SearchReconcileOutcome.RefusedDrift or D1SearchReconcileOutcome.Failed => LogLevel.Warning,
            _ => LogLevel.Information
        };
        logger.Log(level,
            "D1 search reconcile: {Outcome} ({Scan}). D1 {D1Documents}, eligible {Eligible}, missing {Missing}, " +
            "stale {Stale}, orphans {Orphans}, not built {FailedToBuild}, drift {Drift:P2} (threshold {Threshold:P2}); " +
            "upserted {Upserted}, deleted {Deleted}.{Hint}",
            r.Outcome, r.Scan is { } scan ? D1SearchReconcileScanText.Describe(scan, r.SinceTs) : "before the scan",
            r.D1Documents, r.Eligible, r.Missing,
            r.Stale, r.Orphans, r.FailedToBuild, r.Drift,
            _options.DriftThreshold, r.Upserted, r.Deleted,
            r.Outcome == D1SearchReconcileOutcome.RefusedDrift
                ? " Drift is above the threshold: nothing written. Run a full rebuild (SP-2.3), or the D1SearchReconcile " +
                  "console with --write --allow-drift for a deliberate backfill."
                : string.Empty);
        return r;
    }

    private sealed record CatalogueSnapshot(
        IReadOnlyDictionary<string, long> Eligible,
        IReadOnlySet<string> BuildFailed,
        IReadOnlySet<string>? AllEligibleIds,
        long MaxTimestampSeconds,
        IReadOnlyDictionary<string, D1SearchDocument>? HeldDocuments,
        IReadOnlySet<string> ParentChanged)
    {
        public int EligibleCount => AllEligibleIds?.Count ?? Eligible.Count;
    }

    private sealed record ReconcilePlan(D1SearchReconcileReport Report, HashSet<string> Upserts, IReadOnlyList<string> Orphans);
}
