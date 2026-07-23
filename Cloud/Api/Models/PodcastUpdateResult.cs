namespace Api.Models;

public enum PodcastUpdateStatus
{
    Accepted,
    NotFound,
    Failed
}

public record PodcastUpdateResult(
    PodcastUpdateStatus Status,
    Guid? PodcastId = null,
    bool FailureIndexingEpisodes = false,
    bool FailureDeletingFromIndex = false);
