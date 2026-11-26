namespace Api.Models;

public enum NewsReportUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

public record NewsReportUpdateResult(
    NewsReportUpdateStatus Status,
    string? Message = null,
    EpisodeUpdateOutcome? Outcome = null,
    string[]? UnexpectedAdditionalYouTubeIds = null);
