namespace Api.Models;

public enum NewsReportUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Failed
}

public record NewsReportUpdateResult(
    NewsReportUpdateStatus Status,
    string? Message = null);
