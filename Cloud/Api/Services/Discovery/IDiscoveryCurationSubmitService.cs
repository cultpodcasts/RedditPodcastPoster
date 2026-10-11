using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoveryCurationSubmitService
{
    Task<DiscoveryCurationSubmitResult> SubmitAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supervisor apply of a held submission: ingests <paramref name="request"/>'s accepted results from reports
    /// that are <c>PendingSupervisor</c> and promotes those reports to <c>Processed</c>. Never call for unheld reports.
    /// </summary>
    Task<DiscoveryCurationSubmitResult> ApplyHeldAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-submits specific results from already-Processed reports (retry of failed items of an applied submission).
    /// Never claims or changes report state; restamps only the given results.
    /// </summary>
    Task<DiscoveryCurationSubmitResult> RetryItemsAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);
}
