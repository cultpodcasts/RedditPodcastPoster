using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <param name="Targets">Targets to reconcile; null for every usable target.</param>
/// <param name="WriteTargets">Targets that may be written. Every other target is a dry run (report only).</param>
/// <param name="AllowAboveDriftThreshold">
/// Patch even when the drift is above the threshold: the initial backfill (D1 empty, 100% drift) or a deliberate
/// repair. Only the console sets it, and only when asked to.
/// </param>
public sealed record D1SearchReconcileRequest(
    IReadOnlyCollection<string>? Targets,
    IReadOnlyCollection<string> WriteTargets,
    bool AllowAboveDriftThreshold = false)
{
    /// <summary>
    /// The <c>D1SearchReconcile</c> console's command line as a request. <c>--write</c> needs explicit targets, so a
    /// write to the production index is always named on the command line, never implied by configuration. Returns
    /// null with <paramref name="error"/> set for an invalid combination.
    /// </summary>
    public static D1SearchReconcileRequest? FromCommandLine(
        IEnumerable<string> targets,
        bool write,
        bool allowDrift,
        out string? error)
    {
        var named = targets.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        error = (write, allowDrift, named.Length) switch
        {
            (true, _, 0) => "--write needs --target (e.g. --target preview): name the index you mean to write.",
            (false, true, _) => "--allow-drift only applies with --write.",
            _ => null
        };
        return error is not null
            ? null
            : new D1SearchReconcileRequest(named.Length == 0 ? null : named, write ? named : [], allowDrift);
    }

    /// <summary>0 when every target is in sync, patched or reported; 1 when none ran, or one failed or was refused.</summary>
    public static int ExitCode(IReadOnlyList<D1SearchReconcileReport> reports) =>
        reports.Count > 0 && reports.All(r => r.Succeeded) ? 0 : 1;
}

public enum D1SearchReconcileOutcome
{
    /// <summary>Report only: the target is not in the request's write targets.</summary>
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
    string Target,
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
    public bool Succeeded => Outcome is not (D1SearchReconcileOutcome.Failed or D1SearchReconcileOutcome.RefusedDrift);
}

public interface ID1SearchReconciler
{
    /// <summary>
    /// Compares each target's D1 inventory with the Cosmos catalogue and, for write targets within the drift
    /// threshold, upserts missing or stale documents and deletes orphans. Never throws for a D1, Cosmos or network
    /// failure; that target's report says <see cref="D1SearchReconcileOutcome.Failed"/>.
    /// </summary>
    Task<IReadOnlyList<D1SearchReconcileReport>> Reconcile(D1SearchReconcileRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The SP-2.2 (#1322) reconcile. It catches what the push mirror and the hourly drip can't: hard-deleted Cosmos
/// documents (no <c>_ts</c> to see), writes lost in a long outage, and drift of any kind. It's also the initial
/// backfill (an empty D1 is 100% "missing"). Cosmos is the same for every target, so it is scanned <b>once</b> to
/// plan and at most <b>once more</b> to write, however many targets there are.
/// <list type="number">
/// <item>Pass 1 streams the catalogue from <c>_ts</c> 0 and keeps <c>id → modifiedAt</c> of the eligible playables
/// (about 90k longs) and the ids whose document failed to build (neither written nor deleted).</item>
/// <item>Each target's whole inventory (<c>{ id, modifiedAt }</c>) is diffed against that: missing, stale, orphans.
/// A target whose inventory can't be read fails on its own; a partial inventory would turn live documents into
/// orphans.</item>
/// <item>A dry run, an in-sync index or drift above the threshold stops there for that target.</item>
/// <item>Pass 2 (only when some target writes) streams the catalogue again and routes each document to every write
/// target that planned it, 100 per request; then each target's orphans are deleted, 1000 per request. A target whose
/// write fails stops; the others carry on. Documents are never all held in memory. A playable that changed in between
/// is still right: the Worker's <c>modifiedAt</c> guard keeps the newest, and the drip covers the rest.</item>
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

    public async Task<IReadOnlyList<D1SearchReconcileReport>> Reconcile(
        D1SearchReconcileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var usable = writer.UsableTargets;
        var targets = request.Targets is null
            ? usable
            : request.Targets.Where(t => usable.Contains(t, StringComparer.Ordinal)).ToArray();
        foreach (var unknown in request.Targets?.Except(usable, StringComparer.Ordinal) ?? [])
        {
            logger.LogWarning("D1 search reconcile: target '{Target}' is not enabled and configured; skipped.", unknown);
        }

        if (targets.Count == 0)
        {
            return [];
        }

        CatalogueSnapshot snapshot;
        try
        {
            snapshot = await ReadCatalogue(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile could not read Cosmos; nothing was written. {Message}", ex.Message);
            return targets.Select(t => Log(Failed(t, ex.Message))).ToArray();
        }

        var states = new List<TargetState>();
        foreach (var target in targets)
        {
            states.Add(await PlanTarget(target, snapshot, request, cancellationToken));
        }

        var writing = states.Where(s => s.Writes).ToArray();
        if (writing.Length > 0)
        {
            await Apply(writing, cancellationToken);
        }

        return states.Select(s => Log(s.Final())).ToArray();
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

    private async Task<TargetState> PlanTarget(
        string target,
        CatalogueSnapshot snapshot,
        D1SearchReconcileRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, long> inventory;
        try
        {
            inventory = await inventoryReader.ReadInventory(target, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile of target '{Target}' could not read its inventory; nothing was written. {Message}",
                target, ex.Message);
            return TargetState.Done(Failed(target, ex.Message));
        }

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
        var report = new D1SearchReconcileReport(target, D1SearchReconcileOutcome.DryRun, inventory.Count,
            snapshot.Eligible.Count, missing, stale, orphans.Length, snapshot.BuildFailed.Count, drift);

        if (upserts.Count + orphans.Length == 0)
        {
            return TargetState.Done(report with { Outcome = D1SearchReconcileOutcome.InSync });
        }

        if (!request.WriteTargets.Contains(target, StringComparer.Ordinal))
        {
            return TargetState.Done(report);
        }

        if (drift > _options.DriftThreshold && !request.AllowAboveDriftThreshold)
        {
            return TargetState.Done(report with { Outcome = D1SearchReconcileOutcome.RefusedDrift });
        }

        return new TargetState(report, upserts, orphans);
    }

    private async Task Apply(IReadOnlyList<TargetState> states, CancellationToken cancellationToken)
    {
        try
        {
            if (states.Any(s => s.Pending.Count > 0))
            {
                await foreach (var candidate in catalogue.Read(0, cancellationToken).WithCancellation(cancellationToken))
                {
                    if (candidate.Document is not { } document)
                    {
                        continue;
                    }

                    foreach (var state in states)
                    {
                        if (state.Error is null && state.Pending.Remove(candidate.Id))
                        {
                            state.Batch.Add(document);
                            if (state.Batch.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest)
                            {
                                await Flush(state, cancellationToken);
                            }
                        }
                    }

                    if (states.All(s => s.Error is not null || s.Pending.Count == 0))
                    {
                        break;
                    }
                }

                foreach (var state in states.Where(s => s.Error is null))
                {
                    await Flush(state, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile stopped reading Cosmos while writing. {Message}", ex.Message);
            foreach (var state in states.Where(s => s.Error is null))
            {
                state.Error = ex.Message;
            }

            return;
        }

        foreach (var state in states.Where(s => s.Error is null))
        {
            foreach (var chunk in state.Orphans.Chunk(D1SearchIndexWriter.MaxDeletesPerRequest))
            {
                var outcome = (await writer.Write(state.Report.Target, [], chunk, cancellationToken)).Targets.SingleOrDefault();
                state.Deleted += outcome?.Deleted ?? 0;
                if (outcome is not { Succeeded: true })
                {
                    state.Error = outcome?.Error ?? "the write reached no target";
                    break;
                }
            }
        }
    }

    private async Task Flush(TargetState state, CancellationToken cancellationToken)
    {
        if (state.Batch.Count == 0)
        {
            return;
        }

        var outcome = (await writer.Write(state.Report.Target, state.Batch.ToArray(), [], cancellationToken)).Targets
            .SingleOrDefault();
        state.Batch.Clear();
        state.Upserted += outcome?.Upserted ?? 0;
        if (outcome is not { Succeeded: true })
        {
            state.Error = outcome?.Error ?? "the write reached no target";
        }
    }

    private static D1SearchReconcileReport Failed(string target, string error) =>
        new(target, D1SearchReconcileOutcome.Failed, 0, 0, 0, 0, 0, 0, 0, Error: error);

    private D1SearchReconcileReport Log(D1SearchReconcileReport r)
    {
        var level = r.Outcome switch
        {
            D1SearchReconcileOutcome.RefusedDrift or D1SearchReconcileOutcome.Failed => LogLevel.Warning,
            _ => LogLevel.Information
        };
        logger.Log(level,
            "D1 search reconcile of target '{Target}': {Outcome}. D1 {D1Documents}, eligible {Eligible}, missing {Missing}, " +
            "stale {Stale}, orphans {Orphans}, not built {FailedToBuild}, drift {Drift:P2} (threshold {Threshold:P2}); " +
            "upserted {Upserted}, deleted {Deleted}.{Hint}",
            r.Target, r.Outcome, r.D1Documents, r.Eligible, r.Missing, r.Stale, r.Orphans, r.FailedToBuild, r.Drift,
            _options.DriftThreshold, r.Upserted, r.Deleted,
            r.Outcome == D1SearchReconcileOutcome.RefusedDrift
                ? " Drift is above the threshold: nothing written. Run a full rebuild (SP-2.3), or the D1SearchReconcile " +
                  "console with --write --allow-drift for a deliberate backfill."
                : string.Empty);
        return r;
    }

    private sealed record CatalogueSnapshot(IReadOnlyDictionary<string, long> Eligible, IReadOnlySet<string> BuildFailed);

    /// <summary>One target's plan and, for a write target, its progress through pass 2.</summary>
    private sealed class TargetState(D1SearchReconcileReport report, HashSet<string> pending, IReadOnlyList<string> orphans)
    {
        public D1SearchReconcileReport Report { get; } = report;
        public HashSet<string> Pending { get; } = pending;
        public IReadOnlyList<string> Orphans { get; } = orphans;
        public List<D1SearchDocument> Batch { get; } = new(D1SearchIndexWriter.MaxUpsertsPerRequest);
        public bool Writes { get; private init; } = true;
        public int Upserted { get; set; }
        public int Deleted { get; set; }
        public string? Error { get; set; }

        public static TargetState Done(D1SearchReconcileReport report) => new(report, [], []) { Writes = false };

        public D1SearchReconcileReport Final() => !Writes
            ? Report
            : Report with
            {
                Outcome = Error is null ? D1SearchReconcileOutcome.Patched : D1SearchReconcileOutcome.Failed,
                Upserted = Upserted,
                Deleted = Deleted,
                Error = Error
            };
    }
}
