namespace Api.Models;

public enum PersonUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

public record PersonUpdateResult(
    PersonUpdateStatus Status,
    string? Message = null,
    string? ConflictName = null);
