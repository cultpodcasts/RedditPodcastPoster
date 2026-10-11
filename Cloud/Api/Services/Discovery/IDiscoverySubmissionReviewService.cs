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

    /// <summary>Resubmits only Failed items of a partially/failed applied submission; optionally abandons some.</summary>
    Task<DiscoverySubmissionCommandResult> Retry(Guid submissionId, DiscoverySubmissionRetryRequest request,
        SupervisionCaller caller, CancellationToken c);

    /// <summary>
    /// Releases a not-yet-ingested submission: its held reports go back to Unprocessed (guarded) and the submission
    /// becomes Released. Ingests nothing.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> Release(Guid submissionId, DiscoverySubmissionReleaseRequest request,
        SupervisionCaller caller, CancellationToken c);

    /// <summary>
    /// Admin only: returns a report stuck in PendingSupervisor with no live (unapplied) submission to Unprocessed.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> ReleaseStuckReport(Guid documentId, DiscoveryReportReleaseRequest request,
        SupervisionCaller caller, CancellationToken c);
}
