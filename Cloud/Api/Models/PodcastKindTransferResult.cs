using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public enum PodcastKindTransferStatus
{
    Accepted,
    NotFound,
    Conflict,
    InvalidTarget,
    Failed
}

public record PodcastKindTransferResult(
    PodcastKindTransferStatus Status,
    Guid? ParentId = null,
    CatalogueParentKind? TargetKind = null,
    int PlayableCount = 0,
    bool FailureIndexingPlayables = false);
