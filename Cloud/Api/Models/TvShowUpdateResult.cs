namespace Api.Models;

public enum TvShowUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    /// <summary>A service id is already used by another submit target; nothing was saved.</summary>
    Conflict,
    Failed
}

public record TvShowUpdateResult(
    TvShowUpdateStatus Status,
    string? Message = null,
    IReadOnlyList<Guid>? ConflictingIds = null);
