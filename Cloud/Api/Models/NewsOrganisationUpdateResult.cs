namespace Api.Models;

public enum NewsOrganisationUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    /// <summary>A service id is already used by another submit target; nothing was saved.</summary>
    Conflict,
    Failed,
    /// <summary>A check against YouTube could not be made; nothing was saved and the request can be retried (503).</summary>
    ServiceUnavailable
}

public record NewsOrganisationUpdateResult(
    NewsOrganisationUpdateStatus Status,
    string? Message = null,
    IReadOnlyList<Guid>? ConflictingIds = null,
    string? Code = null);
