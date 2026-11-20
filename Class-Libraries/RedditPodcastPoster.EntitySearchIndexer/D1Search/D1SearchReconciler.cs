using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <param name="Write">Patch the index. Without it this is a dry run (report only).</param>
/// <param name="AllowAboveDriftThreshold">
/// Patch even when the drift is above the threshold: the initial backfill (D1 empty, 100% drift) or a deliberate
/// repair. Only the console sets it, and only when asked to.
/// </param>
public sealed record D1SearchReconcileRequest(bool Write, bool AllowAboveDriftThreshold = false)
{
    /// <summary>
    /// The <c>D1SearchReconcile</c> console's command line as a request. Returns null with <paramref name="error"/>
    /// set for an invalid combination.
    /// </summary>
    public static D1SearchReconcileRequest? FromCommandLine(bool write, bool allowDrift, out string? error)
    {
        error = !write && allowDrift ? "--allow-drift only applies with --write." : null;
        return error is not null ? null : new D1SearchReconcileRequest(write, allowDrift);
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

/// <param name="Missing">Eligible in Cosmos, absent from D1.</param>
/// <param name="Stale">In D1 with an older <c>modifiedAt</c> than the Cosmos <c>_ts</c>.</param>
/// <param name="Orphans">In D1 but not eligible in Cosmos (removed, ineligible, hard-deleted, or podcast gone).</param>
/// <param name="Drift">(Missing + Stale + Orphans) / max(D1 count, eligible count).</param>
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
    string? Error = null)
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
    /// </summary>
    Task<D1SearchReconcileReport> Reconcile(D1SearchReconcileRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The SP-2.2 (#1322) reconcile of the one D1 search index (production in the Functions; whatever <c>d1Search</c>
/// points at for the console). It catches what the push mirror and the hourly drip can't: hard-deleted Cosmos
/// documents (no <c>_ts</c> to see), writes lost in a long outage, and drift of any kind. It's also the initial
/// backfill (an empty D1 is 100% "missing").
/// <list type="number">
/// <item>Pass 1 streams the catalogue from <c>_ts</c> 0 and keeps <c>id → modifiedAt</c> of the eligible playables
/// (about 90k longs) and the ids whose document failed to build (neither written nor deleted).</item>
/// <item>The whole D1 inventory (<c>{ id, modifiedAt }</c>) is diffed against that: missing, stale, orphans. If the
/// inventory can't be read the reconcile fails; a partial inventory would turn live documents into orphans.</item>
/// <item>A dry run, an in-sync index or drift above the threshold stops there.</item>
/// <item>Pass 2 streams the catalogue again and sends the planned documents, 100 per request; then the orphans are
/// deleted, 1000 per request. A failed write stops the run. Documents are never all held in memory. A playable that
/// changed in between is still right: the Worker's <c>modifiedAt</c> guard keeps the newest, and the drip covers the
/// rest.</item>
/// </list>
/// </summary>
public sealed class D1SearchReconciler(
    ID1SearchCatalogue catalogue,
    ID1SearchInventoryReader inventoryReader,
    ID1SearchIndexWriter writer,
    IOptions<D1SearchOptions> options,
    ILogger<D1SearchReconciler> logger) : ID1SearchReconciler
{
    private readonly D1SearchReconcileOptions _options = options.Value.Reconcile;

    public async Task<D1SearchReconcileReport> Reconcile(
        D1SearchReconcileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!writer.IsActive)
        {
            return Log(new D1SearchReconcileReport(D1SearchReconcileOutcome.NotConfigured, 0, 0, 0, 0, 0, 0, 0));
        }

        CatalogueSnapshot snapshot;
        try
        {
            snapshot = await ReadCatalogue(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read Cosmos; nothing was written. {Message}", ex.Message);
            return Log(Failed(ex.Message));
        }

        IReadOnlyDictionary<string, long> inventory;
        try
        {
            inventory = await inventoryReader.ReadInventory(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read the D1 inventory; nothing was written. {Message}", ex.Message);
            return Log(Failed(ex.Message, snapshot.Eligible.Count, snapshot.BuildFailed.Count));
        }

        var plan = Plan(snapshot, inventory);
        var report = plan.Report;
        if (plan.Upserts.Count + plan.Orphans.Count == 0)
        {
            return Log(report with { Outcome = D1SearchReconcileOutcome.InSync });
        }

        if (!request.Write)
        {
            return Log(report);
        }

        if (report.Drift > _options.DriftThreshold && !request.AllowAboveDriftThreshold)
        {
            return Log(report with { Outcome = D1SearchReconcileOutcome.RefusedDrift });
        }

        var (upserted, deleted, error) = await Apply(plan, cancellationToken);
        return Log(report with
        {
            Outcome = error is null ? D1SearchReconcileOutcome.Patched : D1SearchReconcileOutcome.Failed,
            Upserted = upserted,
            Deleted = deleted,
            Error = error
        });
    }

    private async Task<CatalogueSnapshot> ReadCatalogue(CancellationToken cancellationToken)
    {
        var eligible = new Dictionary<string, long>(StringComparer.Ordinal);
        var buildFailed = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var candidate in catalogue.Read(0, cancellationToken).WithCancellation(cancellationToken))
        {
            if (candidate.BuildFailed)
            {
                buildFailed.Add(candidate.Id);
            }
            else if (candidate.Document is { } document)
            {
                // A kind change can bring an id twice; the first document wins, as pass 2 sends the first too.
                eligible.TryAdd(candidate.Id, document.ModifiedAt);
            }
        }

        return new CatalogueSnapshot(eligible, buildFailed);
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
            else if (stored < modifiedAt)
            {
                stale++;
                upserts.Add(id);
            }
        }

        var orphans = inventory.Keys
            .Where(id => !snapshot.Eligible.ContainsKey(id) && !snapshot.BuildFailed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var drift = (double)(missing + stale + orphans.Length) / Math.Max(1, Math.Max(inventory.Count, snapshot.Eligible.Count));
        var report = new D1SearchReconcileReport(D1SearchReconcileOutcome.DryRun, inventory.Count,
            snapshot.Eligible.Count, missing, stale, orphans.Length, snapshot.BuildFailed.Count, drift);
        return new ReconcilePlan(report, upserts, orphans);
    }

    private async Task<(int Upserted, int Deleted, string? Error)> Apply(ReconcilePlan plan, CancellationToken cancellationToken)
    {
        int upserted = 0, deleted = 0;
        var pending = plan.Upserts;
        var batch = new List<D1SearchDocument>(D1SearchIndexWriter.MaxUpsertsPerRequest);
        try
        {
            if (pending.Count > 0)
            {
                await foreach (var candidate in catalogue.Read(0, cancellationToken).WithCancellation(cancellationToken))
                {
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

        foreach (var chunk in plan.Orphans.Chunk(D1SearchIndexWriter.MaxDeletesPerRequest))
        {
            var result = await writer.Write([], chunk, cancellationToken);
            deleted += result.Deleted;
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
            return result.Succeeded ? null : result.Error ?? "the write failed";
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
            "D1 search reconcile: {Outcome}. D1 {D1Documents}, eligible {Eligible}, missing {Missing}, " +
            "stale {Stale}, orphans {Orphans}, not built {FailedToBuild}, drift {Drift:P2} (threshold {Threshold:P2}); " +
            "upserted {Upserted}, deleted {Deleted}.{Hint}",
            r.Outcome, r.D1Documents, r.Eligible, r.Missing, r.Stale, r.Orphans, r.FailedToBuild, r.Drift,
            _options.DriftThreshold, r.Upserted, r.Deleted,
            r.Outcome == D1SearchReconcileOutcome.RefusedDrift
                ? " Drift is above the threshold: nothing written. Run a full rebuild (SP-2.3), or the D1SearchReconcile " +
                  "console with --write --allow-drift for a deliberate backfill."
                : string.Empty);
        return r;
    }

    private sealed record CatalogueSnapshot(IReadOnlyDictionary<string, long> Eligible, IReadOnlySet<string> BuildFailed);

    private sealed record ReconcilePlan(D1SearchReconcileReport Report, HashSet<string> Upserts, IReadOnlyList<string> Orphans);
}
