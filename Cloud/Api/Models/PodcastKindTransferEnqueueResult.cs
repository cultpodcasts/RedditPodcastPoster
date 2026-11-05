using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public enum PodcastKindTransferEnqueueStatus
{
    Queued,
    NotFound,
    InvalidTarget,
    AlreadyRunning,
    Failed
}

public record PodcastKindTransferEnqueueResult(
    PodcastKindTransferEnqueueStatus Status,
    Guid? ParentId = null,
    CatalogueParentKind? TargetKind = null,
    DateTime? EnqueuedUtc = null);
