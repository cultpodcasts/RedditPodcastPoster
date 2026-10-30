namespace Api.Models;

public enum PodcastUpdateStatus
{
    Accepted,
    NotFound,
    Failed,
    BadRequest,
    /// <summary>A check against YouTube could not be made; nothing was saved and the request can be retried (503).</summary>
    ServiceUnavailable
}

public record PodcastUpdateResult(
    PodcastUpdateStatus Status,
    Guid? PodcastId = null,
    bool FailureIndexingEpisodes = false,
    bool FailureDeletingFromIndex = false,
    string? Error = null,
    string? Code = null);
