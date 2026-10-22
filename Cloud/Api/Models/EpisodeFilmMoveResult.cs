namespace Api.Models;

public enum EpisodeFilmMoveStatus
{
    Accepted,
    NotFound,
    /// <summary>A film already has the episode's id, or another film already has the name.</summary>
    Conflict,
    /// <summary>The title is invalid (contains '/'), or the episode is removed.</summary>
    BadRequest,
    Failed
}

public record EpisodeFilmMoveResult(
    EpisodeFilmMoveStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false);
