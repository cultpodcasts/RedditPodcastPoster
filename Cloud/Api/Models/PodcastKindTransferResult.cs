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
    AlreadyRunning
}

public record PodcastKindTransferResult(
    PodcastKindTransferStatus Status,
    Guid? ParentId = null,
    CatalogueParentKind? TargetKind = null,
    int PlayableCount = 0,
    bool FailureIndexingPlayables = false);
