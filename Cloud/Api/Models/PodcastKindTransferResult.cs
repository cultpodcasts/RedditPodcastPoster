using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public enum PodcastKindTransferStatus
{
    Accepted,
    NotFound,
    Conflict,
    InvalidTarget,
    Failed,

    /// <summary>Another transfer of the same podcast holds the single-flight marker.</summary>
    AlreadyRunning,

    /// <summary>
    /// Episodes kept arriving during the transfer, so the podcast row was kept (nothing orphaned). Re-run the same
    /// transfer; it resumes because the destination parent exists.
    /// </summary>
    Incomplete
}

public record PodcastKindTransferResult(
    PodcastKindTransferStatus Status,
    Guid? ParentId = null,
    CatalogueParentKind? TargetKind = null,
    int PlayableCount = 0,
    bool FailureIndexingPlayables = false);
