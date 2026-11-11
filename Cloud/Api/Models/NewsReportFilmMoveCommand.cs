namespace Api.Models;

/// <summary>Move one news report into a new film (#1368). Same body as Move to film for a podcast episode.</summary>
public record NewsReportFilmMoveCommand(Guid ReportId, EpisodeFilmMoveRequest Request);
