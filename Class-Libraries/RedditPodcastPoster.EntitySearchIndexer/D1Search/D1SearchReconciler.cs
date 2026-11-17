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
/// backfill (an empty D1 is 100% "missing").
/// <list type="number">
/// <item>Read the target's whole inventory (<c>{ id, modifiedAt }</c>, about 90k small entries). A partial inventory
/// fails the target: it would turn live documents into orphans.</item>
/// <item>Pass 1 streams the catalogue from <c>_ts</c> 0 and keeps only ids: eligible, missing and stale. Orphans are
/// the inventory ids that aren't eligible. A candidate whose document failed to build is neither written nor deleted.
/// </item>
/// <item>A dry run, an in-sync index or drift above the threshold stops here.</item>
/// <item>Pass 2 streams the catalogue again and upserts only the planned ids, 100 per request; then orphans are
/// deleted, 1000 per request. Documents are never all held in memory. A playable that changed in between is still
/// right: the Worker's <c>modifiedAt</c> guard keeps the newest, and the drip covers the rest.</item>
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

        var reports = new List<D1SearchReconcileReport>();
        foreach (var target in targets)
        {
            var write = request.WriteTargets.Contains(target, StringComparer.Ordinal);
            reports.Add(await ReconcileTarget(target, write, request.AllowAboveDriftThreshold, cancellationToken));
        }

        return reports;
    }

    private async Task<D1SearchReconcileReport> ReconcileTarget(
        string target,
        bool write,
        bool allowAboveDriftThreshold,
        CancellationToken cancellationToken)
    {
        D1SearchReconcileReport report;
        try
        {
            var inventory = await inventoryReader.ReadInventory(target, cancellationToken);
            var plan = await Plan(inventory, cancellationToken);
            report = plan.ToReport(target, inventory.Count, D1SearchReconcileOutcome.DryRun);

            if (plan.Missing + plan.Stale + plan.Orphans.Count == 0)
            {
                report = report with { Outcome = D1SearchReconcileOutcome.InSync };
            }
            else if (!write)
            {
                // Stays DryRun.
            }
            else if (report.Drift > _options.DriftThreshold && !allowAboveDriftThreshold)
            {
                report = report with { Outcome = D1SearchReconcileOutcome.RefusedDrift };
            }
            else
            {
                var (upserted, deleted, error) = await Apply(target, plan, cancellationToken);
                report = report with
                {
                    Outcome = error is null ? D1SearchReconcileOutcome.Patched : D1SearchReconcileOutcome.Failed,
                    Upserted = upserted,
                    Deleted = deleted,
                    Error = error
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "D1 search reconcile of target '{Target}' failed; nothing more was written. {Message}",
                target, ex.Message);
            return new D1SearchReconcileReport(target, D1SearchReconcileOutcome.Failed, 0, 0, 0, 0, 0, 0, 0, Error: ex.Message);
        }

        Log(report);
        return report;
    }

    private async Task<ReconcilePlan> Plan(IReadOnlyDictionary<string, long> inventory, CancellationToken cancellationToken)
    {
        var eligible = new HashSet<string>(StringComparer.Ordinal);
        var buildFailed = new HashSet<string>(StringComparer.Ordinal);
        var upserts = new HashSet<string>(StringComparer.Ordinal);
        int missing = 0, stale = 0;
        await foreach (var candidate in catalogue.Read(0, cancellationToken).WithCancellation(cancellationToken))
        {
            if (candidate.BuildFailed)
            {
                buildFailed.Add(candidate.Id);
                continue;
            }

            if (candidate.Document is not { } document || !eligible.Add(candidate.Id))
            {
                continue;
            }

            if (!inventory.TryGetValue(candidate.Id, out var modifiedAt))
            {
                missing++;
                upserts.Add(candidate.Id);
            }
            else if (modifiedAt < document.ModifiedAt)
            {
                stale++;
                upserts.Add(candidate.Id);
            }
        }

        var orphans = inventory.Keys
            .Where(id => !eligible.Contains(id) && !buildFailed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return new ReconcilePlan(eligible.Count, missing, stale, orphans, upserts, buildFailed.Count);
    }

    private async Task<(int Upserted, int Deleted, string? Error)> Apply(
        string target,
        ReconcilePlan plan,
        CancellationToken cancellationToken)
    {
        int upserted = 0, deleted = 0;
        var pending = new HashSet<string>(plan.Upserts, StringComparer.Ordinal);
        var batch = new List<D1SearchDocument>(D1SearchIndexWriter.MaxUpsertsPerRequest);

        async Task<string?> Flush()
        {
            if (batch.Count == 0)
            {
                return null;
            }

            var result = await writer.Write(target, batch.ToArray(), [], cancellationToken);
            batch.Clear();
            var outcome = result.Targets.SingleOrDefault();
            upserted += outcome?.Upserted ?? 0;
            return outcome is { Succeeded: true } ? null : outcome?.Error ?? "the write reached no target";
        }

        if (pending.Count > 0)
        {
            await foreach (var candidate in catalogue.Read(0, cancellationToken).WithCancellation(cancellationToken))
            {
                if (candidate.Document is not { } document || !pending.Remove(candidate.Id))
                {
                    continue;
                }

                batch.Add(document);
                if (batch.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest && await Flush() is { } error)
                {
                    return (upserted, deleted, error);
                }
            }

            if (await Flush() is { } last)
            {
                return (upserted, deleted, last);
            }
        }

        foreach (var chunk in plan.Orphans.Chunk(D1SearchIndexWriter.MaxDeletesPerRequest))
        {
            var result = await writer.Write(target, [], chunk, cancellationToken);
            var outcome = result.Targets.SingleOrDefault();
            deleted += outcome?.Deleted ?? 0;
            if (outcome is not { Succeeded: true })
            {
                return (upserted, deleted, outcome?.Error ?? "the write reached no target");
            }
        }

        return (upserted, deleted, null);
    }

    private void Log(D1SearchReconcileReport r)
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
    }

    private sealed record ReconcilePlan(
        int Eligible,
        int Missing,
        int Stale,
        IReadOnlyList<string> Orphans,
        IReadOnlySet<string> Upserts,
        int FailedToBuild)
    {
        public D1SearchReconcileReport ToReport(string target, int d1Documents, D1SearchReconcileOutcome outcome)
        {
            var drift = (double)(Missing + Stale + Orphans.Count) / Math.Max(1, Math.Max(d1Documents, Eligible));
            return new D1SearchReconcileReport(target, outcome, d1Documents, Eligible, Missing, Stale, Orphans.Count,
                FailedToBuild, drift);
        }
    }
}
