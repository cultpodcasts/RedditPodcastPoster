namespace Api.Models;

public enum EpisodeUpdateStatus
{
    Accepted,
    NotFound,
    Failed,

    /// <summary>The change is not valid for this episode (<see cref="EpisodeUpdateResult.Message"/> says why).</summary>
    BadRequest
}

public record EpisodeUpdateResult(
    EpisodeUpdateStatus Status,
    EpisodeUpdateOutcome? Outcome = null,
    string? Message = null);
