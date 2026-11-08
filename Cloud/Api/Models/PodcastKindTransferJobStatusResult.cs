using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public enum PodcastKindTransferJobQueryStatus
{
    /// <summary>No job recorded and no evidence of a completed transfer for this id.</summary>
    Idle,

    Queued,
    Running,
    Completed,
    Failed,
    Incomplete,

    /// <summary>The podcast id is unknown and no job/completed dest parent was found.</summary>
    NotFound
}

public record PodcastKindTransferJobStatusResult(
    PodcastKindTransferJobQueryStatus Status,
    Guid? ParentId = null,
    CatalogueParentKind? TargetKind = null,
    int PlayableCount = 0,
    bool FailureIndexingPlayables = false,
    string? FailureReason = null,
    DateTime? EnqueuedUtc = null,
    DateTime? StartedUtc = null,
    DateTime? CompletedUtc = null);
