using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;

namespace Indexer.Triggers;

/// <summary>
/// SP-2.2 (#1322): the weekly Cosmos-to-D1 search reconcile, Sunday 04:30 UTC, after the hourly runs have settled.
/// <b>Dry-run unless configured:</b> it writes only to the targets in <c>d1Search:Reconcile:WriteTargets</c> (empty
/// by default), and never above the drift threshold. The initial backfill is too long for this function's 5-minute
/// limit and needs <c>--allow-drift</c>, so it runs from the <c>D1SearchReconcile</c> console. When no target is
/// configured, this does nothing.
/// Never throws: a D1 problem must not page anyone through a failed Indexer run; the outcome is in the log.
/// </summary>
public class D1SearchReconcileTrigger(
    ID1SearchReconciler reconciler,
    ID1SearchIndexWriter writer,
    IOptions<D1SearchOptions> options,
    ILogger<D1SearchReconcileTrigger> logger)
{
    /// <summary>Leaves headroom inside the Indexer's 5-minute <c>functionTimeout</c>.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromMinutes(4.5);

    [Function("D1SearchReconcile")]
    public async Task Run(
        [TimerTrigger("0 30 4 * * 0"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        await Reconcile(cancellationToken);
    }

    public async Task Reconcile(CancellationToken cancellationToken)
    {
        if (!writer.IsActive)
        {
            logger.LogInformation("D1SearchReconcile: no D1 search target is enabled and configured; nothing to do.");
            return;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);
        try
        {
            var request = new D1SearchReconcileRequest(null, options.Value.Reconcile.WriteTargets);
            var reports = await reconciler.Reconcile(request, budget.Token);
            logger.LogInformation("D1SearchReconcile completed: {Reports}.",
                string.Join("; ", reports.Select(r => $"{r.Target}={r.Outcome}")));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "D1SearchReconcile stopped at its {Budget} budget before finishing. The index is unchanged where the " +
                "reconcile had not reached; run the D1SearchReconcile console for a full pass.", Budget);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "D1SearchReconcile failed; Azure indexing is unaffected.");
        }
    }
}
