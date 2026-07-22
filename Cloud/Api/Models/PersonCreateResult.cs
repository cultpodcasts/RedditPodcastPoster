using Api.Dtos;

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
    Person? Person = null,
    string? Message = null,
    string? ConflictName = null);
