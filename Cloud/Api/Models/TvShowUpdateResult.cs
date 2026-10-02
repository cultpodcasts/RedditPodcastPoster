namespace Api.Models;

public enum TvShowUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Failed
}

public record TvShowUpdateResult(
    TvShowUpdateStatus Status,
    string? Message = null);
