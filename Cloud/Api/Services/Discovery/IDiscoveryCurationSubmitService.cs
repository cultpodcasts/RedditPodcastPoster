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
}
