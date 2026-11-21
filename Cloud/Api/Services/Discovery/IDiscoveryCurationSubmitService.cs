using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoveryCurationSubmitService
{
    Task<DiscoveryCurationSubmitResult> SubmitAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads each item and asks for any language, kind or identity decision without persisting or claiming anything.
    /// Returns <c>null</c> when nothing needs deciding (the caller may ingest); otherwise the terminal answer for the
    /// request: a decision hold, <c>Ok</c> with no results when every item was cancelled, or <c>Failed</c> when the
    /// probe itself failed. Each item is probed once.
    /// </summary>
    Task<DiscoveryCurationSubmitResult?> PreflightAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supervisor apply of a held submission: ingests <paramref name="request"/>'s accepted results from reports
    /// that are <c>PendingSupervisor</c> and promotes those reports to <c>Processed</c>. Never call for unheld reports.
    /// Does not probe: run <see cref="PreflightAsync"/> first (the apply request does, before it queues the ingest).
    /// </summary>
    Task<DiscoveryCurationSubmitResult> ApplyHeldAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-submits specific results from already-Processed reports (retry of failed items of an applied submission).
    /// Never claims or changes report state; restamps only the given results. Does not probe: run
    /// <see cref="PreflightAsync"/> first.
    /// </summary>
    Task<DiscoveryCurationSubmitResult> RetryItemsAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);
}
