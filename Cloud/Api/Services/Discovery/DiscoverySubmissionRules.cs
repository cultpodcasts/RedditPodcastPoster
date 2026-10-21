using Api.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Services.Discovery;

/// <summary>Pure supervision rules: who may see/review a submission, and the supervisor-vs-submitter diff.</summary>
public static class DiscoverySubmissionRules
{
    /// <summary>
    /// Admin may review anything. A supervisor may review a submission when it has no supervisor mapping,
    /// or the mapping names them. Nobody reviews their own submission (except an admin).
    /// </summary>
    public static bool CanReview(SupervisionCaller caller, DiscoverySubmission submission)
    {
        if (caller.IsAdmin)
        {
            return true;
        }

        if (!caller.IsSupervisor || caller.Subject == submission.SubmitterSubject)
        {
            return false;
        }

        return submission.SupervisorSubjects is not { Length: > 0 } ||
               submission.SupervisorSubjects.Contains(caller.Subject, StringComparer.Ordinal);
    }

    public static bool CanView(SupervisionCaller caller, DiscoverySubmission submission) =>
        caller.Subject == submission.SubmitterSubject || CanReview(caller, submission);

    /// <summary>
    /// Review/apply is possible only while nothing has been ingested: Pending, or ApplyFailed before any submit
    /// (no item outcomes). Once ingestion was attempted, only retry/abandon of failed items applies.
    /// </summary>
    public static bool IsReviewable(DiscoverySubmission submission) =>
        submission.Status == DiscoverySubmissionStatus.Pending ||
        submission.Status == DiscoverySubmissionStatus.ApplyFailed && submission.ItemOutcomes == null;

    /// <summary>Retry/abandon is possible when an apply ingested and some items are still Failed.</summary>
    /// <summary>Applying is never retryable (a retry/apply already holds it).</summary>
    public static bool IsRetryable(DiscoverySubmission submission) =>
        submission.Status is DiscoverySubmissionStatus.PartiallyApplied or DiscoverySubmissionStatus.ApplyFailed &&
        submission.ItemOutcomes?.Any(x => x.State == DiscoverySubmissionItemState.Failed) == true;

    /// <summary>Applied when nothing is Failed; PartiallyApplied when some succeeded; otherwise ApplyFailed.</summary>
    public static DiscoverySubmissionStatus DeriveStatus(IReadOnlyCollection<DiscoverySubmissionItemOutcome> outcomes) =>
        outcomes.All(x => x.State != DiscoverySubmissionItemState.Failed)
            ? DiscoverySubmissionStatus.Applied
            : outcomes.Any(x => x.State == DiscoverySubmissionItemState.Succeeded)
                ? DiscoverySubmissionStatus.PartiallyApplied
                : DiscoverySubmissionStatus.ApplyFailed;

    /// <summary>Added = final minus submitter's; removed = submitter's minus final. Distinct, order of first appearance.</summary>
    public static DiscoverySubmissionDiff ComputeDiff(IEnumerable<Guid> submitterAccepted, IEnumerable<Guid> finalAccepted)
    {
        var submitter = submitterAccepted.Distinct().ToArray();
        var final = finalAccepted.Distinct().ToArray();
        return new DiscoverySubmissionDiff(
            final.Except(submitter).ToArray(),
            submitter.Except(final).ToArray());
    }
}
