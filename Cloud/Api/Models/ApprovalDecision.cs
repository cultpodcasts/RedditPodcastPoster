namespace Api.Models;

/// <summary>
/// #1484: a curator's decision on an episode awaiting approval (a split-off), sent on the episode edit API as
/// <c>approvalDecision</c> (<c>"Approve"</c> / <c>"Reject"</c>). Request-only: the stored
/// <see cref="RedditPodcastPoster.Models.Episodes.ApprovalStatus"/> has no approved value (null means approved), so
/// approving clears it and rejecting sets <see cref="RedditPodcastPoster.Models.Episodes.ApprovalStatus.Rejected"/>.
/// </summary>
public enum ApprovalDecision
{
    Approve,
    Reject
}
