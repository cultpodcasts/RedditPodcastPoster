using Api.Models;

namespace Api.Services.Discovery;

/// <summary>Supervisor review of held discovery submissions (queries and commands).</summary>
public interface IDiscoverySubmissionReviewService
{
    /// <summary>Supervisor/admin: reviewable queue (Pending + ApplyFailed) they may review. Otherwise: caller's own.</summary>
    Task<DiscoverySubmissionQueryResult<DiscoverySubmissionList>> List(SupervisionCaller caller, CancellationToken c);

    Task<DiscoverySubmissionQueryResult<DiscoverySubmissionDetail>> Get(Guid submissionId, SupervisionCaller caller,
        CancellationToken c);

    Task<DiscoverySubmissionCommandResult> SaveReview(Guid submissionId, DiscoverySubmissionReviewRequest request,
        SupervisionCaller caller, CancellationToken c);

    Task<DiscoverySubmissionCommandResult> Apply(Guid submissionId, DiscoverySubmissionApplyRequest request,
        SupervisionCaller caller, CancellationToken c);
}
