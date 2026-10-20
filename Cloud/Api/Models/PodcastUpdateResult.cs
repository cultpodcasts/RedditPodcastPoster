namespace Api.Models;

public enum PodcastUpdateStatus
{
    Accepted,
    NotFound,
    Failed,
    BadRequest
}

public record PodcastUpdateResult(
    PodcastUpdateStatus Status,
    Guid? PodcastId = null,
    bool FailureIndexingEpisodes = false,
    bool FailureDeletingFromIndex = false,
    string? Error = null);
