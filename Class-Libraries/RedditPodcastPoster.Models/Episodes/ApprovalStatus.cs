namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Curator approval state of an episode that must be reviewed before it is public (#1484): today, an episode split
/// off a stored episode by a submit overwrite (#1416). <b>Null (absent) means approved</b>: every ordinary episode,
/// every existing document (no migration) and a split-off once a curator approves it. Stored in Cosmos and sent by the
/// API as the enum name (<c>"AwaitingApproval"</c>, <c>"Rejected"</c>), like the other stored enums, so it can be
/// queried (<see cref="Episode.CosmosAwaitingApprovalSql"/>).
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Not reviewed yet: hidden from the public and never posted until a curator approves it (clears this).</summary>
    AwaitingApproval,

    /// <summary>A curator rejected it: it stays hidden and is never posted.</summary>
    Rejected
}

public static class ApprovalStatusExtensions
{
    /// <summary>
    /// True for any value (<see cref="ApprovalStatus.AwaitingApproval"/> or <see cref="ApprovalStatus.Rejected"/>):
    /// the episode is held out of public pages, search, D1, the homepage and posting whatever its other flags say.
    /// Null means approved.
    /// </summary>
    public static bool IsHeld(this ApprovalStatus? status) => status is not null;
}
