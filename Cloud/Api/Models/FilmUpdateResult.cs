namespace Api.Models;

public enum FilmUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

public record FilmUpdateResult(
    FilmUpdateStatus Status,
    string? Message = null,
    EpisodeUpdateOutcome? Outcome = null,
    Guid[]? UnexpectedTrailerIds = null);
