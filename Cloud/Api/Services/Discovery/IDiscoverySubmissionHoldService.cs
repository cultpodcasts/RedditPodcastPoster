using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoverySubmissionHoldService
{
    /// <summary>
    /// Records a supervised curator's discovery submit for supervisor review without applying it,
    /// and moves the reports to <c>PendingSupervisor</c> so nobody else can curate them meanwhile.
    /// </summary>
    Task<DiscoverySubmissionHoldResult> HoldAsync(
        DiscoverySubmitRequest request,
        string submitterSubject,
        IReadOnlyList<string> supervisorSubjects,
        CancellationToken cancellationToken);
}
