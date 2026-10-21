namespace Api.Models;

public enum SubjectCreateStatus
{
    Accepted,
    BadRequest,
    Conflict,
    Failed
}

public record SubjectCreateResult(
    SubjectCreateStatus Status,
    string? ConflictName = null,
    string? Message = null);
