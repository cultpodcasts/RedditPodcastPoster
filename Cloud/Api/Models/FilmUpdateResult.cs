namespace Api.Models;

public enum FilmUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Failed
}

public record FilmUpdateResult(
    FilmUpdateStatus Status,
    string? Message = null);
