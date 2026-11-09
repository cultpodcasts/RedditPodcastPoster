namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>
/// Run-time scope of a single podcast (or catalogue parent) update: the per-podcast budget
/// <see cref="CancellationToken"/> and the <see cref="IPodcastIndexingStageReporter"/> that traces the stage the run
/// reached (#1070, #1073).
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately <b>not</b> part of <see cref="IndexingContext"/>. <see cref="IndexingContext"/> is a value-equality
/// record of indexing <i>policy</i> flags that is copied with <c>with</c> and compared; a cancellation token and a
/// mutable, logger-backed reporter are neither policy nor values, and each belongs to exactly one podcast's run.
/// </para>
/// <para>
/// The indexer loops (<c>PodcastsUpdater</c>, <c>CatalogueParentsUpdater</c>) and the on-demand <c>Indexer</c>
/// open a scope with <see cref="Enter(PodcastRunScope)"/> around the call that starts one podcast's update. The
/// scope is ambient (backed by <see cref="AsyncLocal{T}"/>, like <c>Activity.Current</c>): it flows into every
/// await, task and continuation started inside that call, and disposing the handle restores the previous scope on
/// the caller's flow. An update that is abandoned after its budget keeps its own (cancelled) scope, and the next
/// podcast never sees it.
/// </para>
/// <para>
/// Platform code reads <see cref="Current"/>. Outside a scope that is <see cref="None"/>:
/// <see cref="System.Threading.CancellationToken.None"/> and no reporter, which matches the previous
/// <c>IndexingContext</c> defaults for submit, discovery, enrichment and console callers.
/// </para>
/// <para>
/// Handles may be disposed out of order (for example an outer handle ends while an inner one, kept elsewhere, is
/// still open). Disposing a handle whose scope is not the current one on this flow leaves the current scope alone and
/// marks that scope as closed out of order; a later disposal never puts such a scope back, and falls back to the
/// nearest enclosing scope that was not closed out of order, or <see cref="None"/>. A handle disposed in order (the
/// loops dispose theirs as soon as the update has started) is an ordinary pop on the loop's flow: the scope stays
/// live on the update's flow, which captured it, including after a nested scope inside the update is left.
/// </para>
/// <para>
/// Two things must not be done inside a run, because nothing would fail at compile time and the budget would quietly
/// stop applying:
/// <list type="bullet">
/// <item><description>
/// Do not cache a shared <see cref="Task"/> (or start background work from a singleton) whose work reads
/// <see cref="Current"/>. The task keeps the scope of the podcast that started it, so that podcast's budget would
/// cancel it for every later podcast that awaits it. Cache results, not tasks.
/// </description></item>
/// <item><description>
/// Do not start run work with <see cref="ExecutionContext.SuppressFlow"/>, <c>ThreadPool.UnsafeQueueUserWorkItem</c>
/// or anything else that does not flow <see cref="ExecutionContext"/>. That work reads <see cref="None"/>: no
/// budget and no stage reporting.
/// </description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PodcastRunScope
{
    private static readonly AsyncLocal<Frame?> Ambient = new();

    public PodcastRunScope(CancellationToken cancellationToken, IPodcastIndexingStageReporter? stageReporter = null)
    {
        CancellationToken = cancellationToken;
        StageReporter = stageReporter;
    }

    /// <summary>No budget and no stage reporter: what callers outside an indexer run see.</summary>
    public static PodcastRunScope None { get; } = new(CancellationToken.None);

    /// <summary>The scope of the podcast run on the current async flow, or <see cref="None"/>.</summary>
    public static PodcastRunScope Current => Ambient.Value?.Scope ?? None;

    /// <summary>Cancelled when this podcast's update budget runs out.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The podcast's stage reporter; <c>null</c> outside the indexer loops.</summary>
    public IPodcastIndexingStageReporter? StageReporter { get; }

    /// <summary>Reports a stage on <see cref="StageReporter"/> when one is attached.</summary>
    public void ReportStage(string stage, string? detail = null)
    {
        StageReporter?.Stage(stage, detail);
    }

    /// <summary>
    /// Makes <paramref name="scope"/> the <see cref="Current"/> scope on this async flow until the returned handle is
    /// disposed, which restores the nearest enclosing scope that is still open (normally the one that was current
    /// before).
    /// </summary>
    public static IDisposable Enter(PodcastRunScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var frame = new Frame(scope, Ambient.Value);
        Ambient.Value = frame;
        return frame;
    }

    public override string ToString() =>
        $"{nameof(PodcastRunScope)} cancellable: '{CancellationToken.CanBeCanceled}', cancelled: '{CancellationToken.IsCancellationRequested}', stage: '{StageReporter?.CurrentStage ?? "none"}'.";

    /// <summary>
    /// One <see cref="Enter(PodcastRunScope)"/>: the scope it made current and the frame that was current before.
    /// Disposing it only changes the flow when it is the current frame. A frame disposed while it was not current is
    /// closed out of order, and restoring skips such frames, so an out-of-order disposal cannot bring back a stale
    /// (possibly cancelled) scope. A frame disposed in order is not skipped: it is still the live scope on any flow
    /// that captured it (an update that outlives its loop's handle).
    /// </summary>
    private sealed class Frame : IDisposable
    {
        private readonly Frame? _parent;
        private volatile bool _disposed;
        private volatile bool _closedOutOfOrder;

        public Frame(PodcastRunScope scope, Frame? parent)
        {
            Scope = scope;
            _parent = parent;
        }

        public PodcastRunScope Scope { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!ReferenceEquals(Ambient.Value, this))
            {
                // Not this flow's current frame: an inner frame is still open, or the handle is disposed on another
                // flow. Leave the current scope alone; the inner frame skips this one when it is disposed.
                _closedOutOfOrder = true;
                return;
            }

            // Only skip frames closed out of order. A frame popped in order on another flow (the loop's) is still the
            // live scope on this one (the update's) and must be restored after a nested scope ends.
            var restore = _parent;
            while (restore is { _closedOutOfOrder: true })
            {
                restore = restore._parent;
            }

            Ambient.Value = restore;
        }
    }
}
