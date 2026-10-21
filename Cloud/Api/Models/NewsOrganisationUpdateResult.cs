namespace Api.Models;

public enum NewsOrganisationUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Failed
}

public record NewsOrganisationUpdateResult(
    NewsOrganisationUpdateStatus Status,
    string? Message = null);
