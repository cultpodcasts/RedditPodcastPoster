namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Curator approval state of an episode that must be reviewed before it is public (#1484): today, an episode split
/// off a stored episode by a submit overwrite (#1416). Null on every other episode. Stored in Cosmos and sent by the
/// API as the enum name (<c>"AwaitingApproval"</c>, <c>"Approved"</c>, <c>"Rejected"</c>), like the other stored enums,
/// so it can be queried (e.g. <c>e.approvalStatus = "AwaitingApproval"</c>, <see cref="Episode.CosmosAwaitingApprovalSql"/>).
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Not reviewed yet: hidden from the public and never posted until a curator approves it.</summary>
    AwaitingApproval,

    /// <summary>A curator approved it: an ordinary, public episode, posted under the usual rules.</summary>
    Approved,

    /// <summary>A curator rejected it: it stays hidden and is never posted.</summary>
    Rejected
}

public static class ApprovalStatusExtensions
{
    /// <summary>
    /// True for <see cref="ApprovalStatus.AwaitingApproval"/> and <see cref="ApprovalStatus.Rejected"/>: the episode
    /// is held out of public pages, search, D1, the homepage and posting whatever its other flags say.
    /// </summary>
    public static bool IsHeld(this ApprovalStatus? status) =>
        status is ApprovalStatus.AwaitingApproval or ApprovalStatus.Rejected;
}
