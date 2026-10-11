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

    public static bool IsReviewable(DiscoverySubmission submission) =>
        submission.Status is DiscoverySubmissionStatus.Pending or DiscoverySubmissionStatus.ApplyFailed;

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
