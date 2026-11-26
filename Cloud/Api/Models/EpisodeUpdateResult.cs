namespace Api.Models;

public enum EpisodeUpdateStatus
{
    Accepted,
    NotFound,
    Failed,

    /// <summary>The change is not valid for this episode (<see cref="EpisodeUpdateResult.Message"/> says why).</summary>
    BadRequest,

    /// <summary>
    /// The additional YouTube keep-list is stale, or a new id already belongs to another item
    /// (<see cref="EpisodeUpdateResult.Message"/> and <see cref="EpisodeUpdateResult.UnexpectedAdditionalYouTubeIds"/>).
    /// </summary>
    Conflict
}

public record EpisodeUpdateResult(
    EpisodeUpdateStatus Status,
    EpisodeUpdateOutcome? Outcome = null,
    string? Message = null,
    string[]? UnexpectedAdditionalYouTubeIds = null);
