namespace Api.Models;

public enum PersonCreateStatus
{
    Accepted,
    BadRequest,
    Conflict,
    Failed
}

public record PersonCreateResult(
    PersonCreateStatus Status,
    string? Message = null,
    string? ConflictName = null);
