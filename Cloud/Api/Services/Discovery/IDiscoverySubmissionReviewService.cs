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

    /// <summary>
    /// Claims the submission (Applying) and runs the short language / kind / identity preflight. A decision, a
    /// failed probe or an all-cancelled set is answered now; otherwise the ingest is queued and the result is
    /// <see cref="DiscoverySubmissionCommandStatus.Accepted"/> (poll the submission for the outcome, #1415).
    /// </summary>
    Task<DiscoverySubmissionCommandResult> Apply(Guid submissionId, DiscoverySubmissionApplyRequest request,
        SupervisionCaller caller, CancellationToken c);

    /// <summary>
    /// Resubmits only Failed items of a partially/failed applied submission; optionally abandons some. Like
    /// <see cref="Apply"/>, the ingest is queued after the preflight.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> Retry(Guid submissionId, DiscoverySubmissionRetryRequest request,
        SupervisionCaller caller, CancellationToken c);

    /// <summary>
    /// Queue worker: ingests a queued apply / retry and records its outcome. A message for a claim that no longer holds
    /// the submission is a no-op; a redelivery after a run started ends the claim (see <see cref="AbandonPoisoned"/>)
    /// unless the apply's reports were never claimed.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> ProcessQueued(DiscoverySubmissionApplyQueueMessage message,
        CancellationToken c);

    /// <summary>
    /// Poison-queue handler: ends the claim so the submission is never stranded Applying. Unfinished items become
    /// Failed (retryable); an apply whose reports were never claimed becomes ApplyFailed and can be applied again.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> AbandonPoisoned(DiscoverySubmissionApplyQueueMessage message,
        CancellationToken c);

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

    /// <summary>
    /// Admin only: manually sets the submission status (any defined value except None and Applying), clears in-progress fields and
    /// records a StateChanged history entry. Does not change report states or item outcomes.
    /// </summary>
    Task<DiscoverySubmissionCommandResult> SetState(Guid submissionId, DiscoverySubmissionSetStateRequest request,
        SupervisionCaller caller, CancellationToken c);
}
