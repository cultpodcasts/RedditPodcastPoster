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
/// </remarks>
public sealed class PodcastRunScope
{
    private static readonly AsyncLocal<PodcastRunScope?> Ambient = new();

    public PodcastRunScope(CancellationToken cancellationToken, IPodcastIndexingStageReporter? stageReporter = null)
    {
        CancellationToken = cancellationToken;
        StageReporter = stageReporter;
    }

    /// <summary>No budget and no stage reporter: what callers outside an indexer run see.</summary>
    public static PodcastRunScope None { get; } = new(CancellationToken.None);

    /// <summary>The scope of the podcast run on the current async flow, or <see cref="None"/>.</summary>
    public static PodcastRunScope Current => Ambient.Value ?? None;

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
    /// disposed, which restores the scope that was current before.
    /// </summary>
    public static IDisposable Enter(PodcastRunScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var previous = Ambient.Value;
        Ambient.Value = scope;
        return new Restore(previous);
    }

    public override string ToString() =>
        $"{nameof(PodcastRunScope)} cancellable: '{CancellationToken.CanBeCanceled}', cancelled: '{CancellationToken.IsCancellationRequested}', stage: '{StageReporter?.CurrentStage ?? "none"}'.";

    private sealed class Restore(PodcastRunScope? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Ambient.Value = previous;
        }
    }
}
