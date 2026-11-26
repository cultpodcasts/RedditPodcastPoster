using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public enum D1SearchRebuildOutcome
{
    /// <summary>The D1 search sync is not configured (<c>d1Search</c>); nothing was read or written.</summary>
    NotConfigured,

    /// <summary>The new index is live, passed every check, and the old one was dropped.</summary>
    Rebuilt,

    /// <summary>The new index was swapped in but failed a check, so the old index was put back and the new one dropped.</summary>
    SwappedBack,

    /// <summary>
    /// The new index was never swapped in: playables failed to build, or the Worker's verification refused the swap.
    /// The shadow was dropped; the live index is untouched.
    /// </summary>
    Refused,

    /// <summary>
    /// A Cosmos, network or Worker failure. <see cref="D1SearchRebuildReport.Error"/> says what state the index was
    /// left in; rerunning <c>--rebuild</c> resumes or cleans it up.
    /// </summary>
    Failed
}

/// <param name="Loaded">Documents loaded into the new index.</param>
/// <param name="Kinds">Loaded documents per content kind (the Worker's <c>contentKind</c>; Episode when unset).</param>
/// <param name="FailedToBuild">Eligible playables whose search document could not be built (none are loaded then).</param>
/// <param name="Resumed">An earlier run had already swapped this rebuild in; this run only checked and settled it.</param>
/// <param name="Problems">Why the new index was refused or swapped back.</param>
public sealed record D1SearchRebuildReport(
    D1SearchRebuildOutcome Outcome,
    string? RebuildId,
    int Loaded,
    IReadOnlyDictionary<string, int> Kinds,
    int FailedToBuild,
    IReadOnlyList<string> Problems,
    string? Error = null,
    bool Resumed = false,
    D1SearchRebuildCheck? Check = null,
    D1SearchReconcileReport? Reconcile = null)
{
    public bool Succeeded => Outcome == D1SearchRebuildOutcome.Rebuilt;

    public static int ExitCode(D1SearchRebuildReport report) => report.Succeeded ? 0 : 1;
}

public static class D1SearchRebuildCommandLine
{
    /// <summary>
    /// Why <c>--rebuild</c> cannot run with these flags; null when it can. A rebuild always writes, so it needs an
    /// explicit <c>--write</c>, and it always reads the whole catalogue and has its own drift checks.
    /// </summary>
    public static string? Error(bool write, bool allowDrift, bool full) =>
        !write ? "--rebuild writes to the index: add --write to confirm." :
        allowDrift || full ? "--rebuild always reads the whole catalogue and checks drift itself: drop --allow-drift and --full." :
        null;
}

public interface ID1SearchRebuilder
{
    /// <summary>
    /// Rebuilds the D1 search index from Cosmos (SP-2.3 #226). Writes: only the <c>D1SearchReconcile</c> console calls
    /// this, with <c>--rebuild --write</c>. Never throws for a D1, Cosmos or network failure; the report says
    /// <see cref="D1SearchRebuildOutcome.Failed"/>.
    /// </summary>
    Task<D1SearchRebuildReport> Rebuild(
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress = null);
}

/// <summary>
/// The SP-2.3 (#226) rebuild, run on demand from the <c>D1SearchReconcile</c> console (Jon, 20 Nov 2026: a few times a
/// year, for drift or corruption, never scheduled). Uses the api Worker's <c>/search/admin/rebuild</c> routes.
/// <list type="number">
/// <item><b>Prepare:</b> read the rebuild state. A rebuild left <c>loading</c> or <c>swappedBack</c> by an earlier
/// run is aborted (the live index was never changed). One left <c>swapped</c> is resumed: checked and settled as
/// below, without loading again. Then start: the Worker creates empty shadow tables, and from then on every write to
/// the index (push, drip, reconcile) also goes to the shadow.</item>
/// <item><b>Load:</b> stream the whole catalogue (<see cref="ID1SearchCatalogue.Read"/> from <c>_ts</c> 0, the same
/// documents the reconcile builds) into the shadow, 100 per request, each id once, counting per content kind. If any
/// eligible playable fails to build, the shadow is dropped: swapping would lose that document from search.</item>
/// <item><b>Swap:</b> the Worker verifies the shadow (counts within <see cref="D1SearchRebuildOptions.Tolerance"/>
/// of what was loaded, full-text rows, no shrink beyond <see cref="D1SearchRebuildOptions.MaxShrink"/>,
/// full-text structure) and swaps it in with one transaction, keeping the old index as parked tables. A refused swap
/// drops the shadow.</item>
/// <item><b>Check</b>, straight after the swap (Jon: "run checks and drop sooner"): the full-text integrity check
/// against the new table and the sample searches (<see cref="D1SearchRebuildOptions.SampleQueries"/>), then a full
/// reconcile with write. It patches the few documents a delete racing the load may have brought back, and refuses
/// above the drift threshold.</item>
/// <item><b>Settle:</b> every check passed, so the parked old index is dropped. If any check failed, the old index
/// is swapped back and the new one dropped. The Worker writes every push to the parked old index too while the new
/// one is being checked (Api #290), so the restored index has missed nothing and needs no catch-up.</item>
/// </list>
/// </summary>
public sealed class D1SearchRebuilder(
    ID1SearchRebuildClient client,
    ID1SearchIndexWriter writer,
    ID1SearchCatalogue catalogue,
    ID1SearchReconciler reconciler,
    IOptions<D1SearchOptions> options,
    ILogger<D1SearchRebuilder> logger) : ID1SearchRebuilder
{
    private readonly D1SearchRebuildOptions _rebuild = options.Value.Rebuild;

    public async Task<D1SearchRebuildReport> Rebuild(
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress = null)
    {
        if (!writer.IsActive)
        {
            return Log(Report(D1SearchRebuildOutcome.NotConfigured, null, new LoadTally()));
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Prepare, 0, 1, "reading rebuild state"));
        string rebuildId;
        try
        {
            var status = await client.Status(cancellationToken);
            if (status is { State: D1SearchRebuildStatus.Swapped, RebuildId: { } swappedId })
            {
                logger.LogWarning("D1 search rebuild {RebuildId} was already swapped in by an earlier run; checking it.",
                    swappedId);
                progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Prepare, 1, 1, "resuming"));
                return await CheckAndSettle(swappedId, new LoadTally(), true, cancellationToken, progress);
            }

            if (status is { State: D1SearchRebuildStatus.Loading or D1SearchRebuildStatus.SwappedBack, RebuildId: { } staleId })
            {
                logger.LogWarning("D1 search rebuild {RebuildId} was left {State}; aborting it before starting.",
                    staleId, status.State);
                await client.Abort(staleId, cancellationToken);
            }

            rebuildId = await client.Start(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Log(Report(D1SearchRebuildOutcome.Failed, null, new LoadTally(),
                error: $"could not start a rebuild; the live index is unchanged: {ex.Message}"));
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Prepare, 1, 1, rebuildId));
        logger.LogInformation("D1 search rebuild {RebuildId} started.", rebuildId);

        var tally = new LoadTally();
        try
        {
            await Load(rebuildId, tally, cancellationToken, progress);
        }
        catch (Exception ex)
        {
            var dropped = await TryAbort(rebuildId);
            return Log(Report(D1SearchRebuildOutcome.Failed, rebuildId, tally,
                error: $"loading the new index failed; the live index is unchanged{dropped}: {ex.Message}"));
        }

        if (tally.FailedToBuild.Count > 0)
        {
            var dropped = await TryAbort(rebuildId);
            var sample = string.Join(", ", tally.FailedToBuild.Take(10));
            return Log(Report(D1SearchRebuildOutcome.Refused, rebuildId, tally,
                [$"{tally.FailedToBuild.Count} eligible playable(s) failed to build and would be lost from search " +
                 $"(e.g. {sample}); fix them and rerun. Nothing was swapped{dropped}."]));
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Swap, 0, 1, "verifying"));
        var expected = new D1SearchRebuildExpectation(tally.Loaded, tally.Kinds, _rebuild.Tolerance, _rebuild.MaxShrink);
        try
        {
            await client.Swap(rebuildId, expected, cancellationToken);
        }
        catch (Exception ex) when (D1SearchIndexWriter.IsRebuildConflict(ex))
        {
            var dropped = await TryAbort(rebuildId);
            return Log(Report(D1SearchRebuildOutcome.Refused, rebuildId, tally, [$"swap refused{dropped}: {ex.Message}"]));
        }
        catch (Exception ex)
        {
            // The swap may have committed with its answer lost: ask before deciding.
            var state = await TryStatus();
            if (state is not { State: D1SearchRebuildStatus.Swapped } || state.RebuildId != rebuildId)
            {
                var dropped = state is { State: D1SearchRebuildStatus.Loading } && state.RebuildId == rebuildId
                    ? await TryAbort(rebuildId)
                    : string.Empty;
                return Log(Report(D1SearchRebuildOutcome.Failed, rebuildId, tally,
                    error: $"the swap failed (index state now {state?.State ?? "unknown"}){dropped}: {ex.Message}. " +
                           "Rerun --rebuild to resume or clean up."));
            }

            logger.LogWarning("D1 search rebuild {RebuildId}: the swap's answer was lost ({Message}) but it committed.",
                rebuildId, ex.Message);
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Swap, 1, 1, "swapped"));
        logger.LogInformation("D1 search rebuild {RebuildId} swapped in ({Loaded} documents).", rebuildId, tally.Loaded);
        return await CheckAndSettle(rebuildId, tally, false, cancellationToken, progress);
    }

    private async Task Load(
        string rebuildId,
        LoadTally tally,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress)
    {
        int? total = null;
        if (progress is not null)
        {
            progress.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Load, 0, null, "counting"));
            total = await catalogue.CountPlayables(cancellationToken);
            progress.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Load, 0, total));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var batch = new List<D1SearchDocument>(D1SearchIndexWriter.MaxUpsertsPerRequest);
        var read = 0;
        await foreach (var candidate in catalogue.Read(0, cancellationToken))
        {
            read++;
            if (!seen.Add(candidate.Id))
            {
                continue;
            }

            if (candidate.BuildFailed)
            {
                tally.FailedToBuild.Add(candidate.Id);
            }
            else if (candidate.Document is { } document)
            {
                batch.Add(document);
                var kind = string.IsNullOrEmpty(document.ContentKind) ? "Episode" : document.ContentKind;
                tally.Kinds[kind] = tally.Kinds.GetValueOrDefault(kind) + 1;
                if (batch.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest)
                {
                    await Flush(rebuildId, batch, tally, cancellationToken);
                }
            }

            if (read % D1SearchIndexWriter.MaxUpsertsPerRequest == 0)
            {
                progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Load, read, total));
            }
        }

        await Flush(rebuildId, batch, tally, cancellationToken);
        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Load, read, read,
            $"{tally.Loaded.ToString("N0", CultureInfo.InvariantCulture)} loaded"));
    }

    private async Task Flush(string rebuildId, List<D1SearchDocument> batch, LoadTally tally, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        await client.Load(rebuildId, batch.ToArray(), cancellationToken);
        tally.Loaded += batch.Count;
        batch.Clear();
    }

    private async Task<D1SearchRebuildReport> CheckAndSettle(
        string rebuildId,
        LoadTally tally,
        bool resumed,
        CancellationToken cancellationToken,
        IProgress<D1SearchReconcileProgress>? progress)
    {
        var problems = new List<string>();
        D1SearchRebuildCheck? check = null;
        D1SearchReconcileReport? reconcile = null;

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Check, 0, 2, "integrity and samples"));
        try
        {
            check = await client.Check(rebuildId, _rebuild.EffectiveSampleQueries, cancellationToken);
            problems.AddRange(CheckProblems(check));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            problems.Add($"check failed: {ex.Message}");
        }

        if (problems.Count == 0)
        {
            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Check, 1, 2, "reconcile"));
            reconcile = await reconciler.Reconcile(new D1SearchReconcileRequest(Write: true, Full: true), cancellationToken,
                progress);
            if (reconcile.Outcome is not (D1SearchReconcileOutcome.InSync or D1SearchReconcileOutcome.Patched))
            {
                problems.Add(
                    $"reconcile {reconcile.Outcome}: drift {reconcile.Drift:P2} (missing {reconcile.Missing}, " +
                    $"stale {reconcile.Stale}, orphans {reconcile.Orphans}){(reconcile.Error is { } e ? $": {e}" : "")}");
            }
        }

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Check, 2, 2,
            problems.Count == 0 ? "passed" : "FAILED"));

        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Settle, 0, 1,
            problems.Count == 0 ? "dropping old index" : "swapping old index back"));
        if (problems.Count == 0)
        {
            try
            {
                await client.Finish(rebuildId, cancellationToken);
            }
            catch (Exception ex)
            {
                // The drop may have committed with its answer lost.
                var state = await TryStatus();
                if (state is { State: D1SearchRebuildStatus.None })
                {
                    logger.LogWarning("D1 search rebuild {RebuildId}: finish's answer was lost ({Message}) but it committed.",
                        rebuildId, ex.Message);
                }
                else
                {
                    return Log(Report(D1SearchRebuildOutcome.Failed, rebuildId, tally,
                        error: FinishFailed(rebuildId, state, ex.Message),
                        resumed: resumed, check: check, reconcile: reconcile));
                }
            }

            progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Settle, 1, 1, "old index dropped"));
            return Log(Report(D1SearchRebuildOutcome.Rebuilt, rebuildId, tally, resumed: resumed, check: check,
                reconcile: reconcile));
        }

        Exception? swapBackFailure = null;
        try
        {
            await client.SwapBack(rebuildId, cancellationToken);
        }
        catch (Exception ex)
        {
            swapBackFailure = ex;
        }

        if (swapBackFailure is not null)
        {
            // The swap-back may have committed with its answer lost.
            if (await TryStatus() is { State: D1SearchRebuildStatus.SwappedBack } back && back.RebuildId == rebuildId)
            {
                logger.LogWarning("D1 search rebuild {RebuildId}: swap-back's answer was lost ({Message}) but it committed.",
                    rebuildId, swapBackFailure.Message);
            }
            else
            {
                return Log(Report(D1SearchRebuildOutcome.Failed, rebuildId, tally, problems,
                    $"the new index failed a check and swapping the old index back failed: {swapBackFailure.Message}. " +
                    "The new index is still live; rerun --rebuild to check it again, or swap back by hand.",
                    resumed, check, reconcile));
            }
        }

        var dropped = await TryAbort(rebuildId);
        progress?.Report(new D1SearchReconcileProgress(D1SearchReconcilePhase.Settle, 1, 1, "old index back"));
        problems.Add($"{RestoredNote}{dropped}");

        return Log(Report(D1SearchRebuildOutcome.SwappedBack, rebuildId, tally, problems, resumed: resumed, check: check,
            reconcile: reconcile));
    }

    /// <summary>Added to a swapped-back report: the old index is live again and is not stale.</summary>
    public const string RestoredNote =
        "old index restored; it received every write made since the swap (the Worker writes both while checking), " +
        "so it needs no catch-up";

    /// <summary>The operator's next step when finish failed and the rebuild state says it did not commit.</summary>
    public static string FinishFailed(string rebuildId, D1SearchRebuildStatus? state, string message) => state switch
    {
        { State: D1SearchRebuildStatus.Swapped } when state.RebuildId == rebuildId =>
            $"the new index passed every check and is live, but dropping the old index failed: {message}. The rebuild " +
            "is still swapped: rerun --rebuild to check it again and finish.",
        _ =>
            $"the new index passed every check and is live, but dropping the old index failed: {message}, and the rebuild " +
            $"state now reads {state?.State ?? "unknown"}. Check GET /search/admin/rebuild: if it reports none, the old tables were dropped " +
            "and nothing more is needed (a rerun would start a new rebuild); rerun --rebuild only while it still " +
            "reports swapped."
    };

    /// <summary>Why a post-swap check failed; empty when it passed.</summary>
    public static IReadOnlyList<string> CheckProblems(D1SearchRebuildCheck check)
    {
        var problems = new List<string>();
        if (check.Integrity is not { Ok: true })
        {
            problems.Add($"full-text integrity check failed: {check.Integrity?.Error ?? "no result"}");
        }

        var empty = (check.Samples ?? []).Where(s => s.Hits <= 0).Select(s => $"\"{s.Query}\"").ToArray();
        if (empty.Length > 0)
        {
            problems.Add($"sample search(es) found nothing: {string.Join(", ", empty)}");
        }

        if (problems.Count == 0 && !check.Ok)
        {
            problems.Add("the Worker reported the check as failed");
        }

        return problems;
    }

    /// <summary>Drops the shadow; returns a note for the report, empty on success.</summary>
    private async Task<string> TryAbort(string rebuildId)
    {
        try
        {
            await client.Abort(rebuildId, CancellationToken.None);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "D1 search rebuild {RebuildId}: dropping the shadow failed.", rebuildId);
            return $" (dropping the new tables failed: {ex.Message}; rerun --rebuild to clean up)";
        }
    }

    private async Task<D1SearchRebuildStatus?> TryStatus()
    {
        try
        {
            return await client.Status(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "D1 search rebuild: reading the rebuild state failed.");
            return null;
        }
    }

    private static D1SearchRebuildReport Report(
        D1SearchRebuildOutcome outcome,
        string? rebuildId,
        LoadTally tally,
        IReadOnlyList<string>? problems = null,
        string? error = null,
        bool resumed = false,
        D1SearchRebuildCheck? check = null,
        D1SearchReconcileReport? reconcile = null) =>
        new(outcome, rebuildId, tally.Loaded, new Dictionary<string, int>(tally.Kinds), tally.FailedToBuild.Count,
            problems ?? [], error, resumed, check, reconcile);

    private D1SearchRebuildReport Log(D1SearchRebuildReport report)
    {
        var level = report.Succeeded || report.Outcome == D1SearchRebuildOutcome.NotConfigured
            ? LogLevel.Information
            : LogLevel.Error;
        logger.Log(level,
            "D1 search rebuild {RebuildId}: {Outcome}; loaded {Loaded}, failed to build {FailedToBuild}; {Problems}{Error}",
            report.RebuildId ?? "-", report.Outcome, report.Loaded, report.FailedToBuild,
            report.Problems.Count == 0 ? "no problems" : string.Join("; ", report.Problems),
            report.Error is null ? string.Empty : $"; {report.Error}");
        return report;
    }

    private sealed class LoadTally
    {
        public int Loaded { get; set; }
        public Dictionary<string, int> Kinds { get; } = new(StringComparer.Ordinal);
        public List<string> FailedToBuild { get; } = [];
    }
}
