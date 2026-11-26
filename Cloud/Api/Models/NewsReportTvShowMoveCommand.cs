namespace Api.Models;

/// <summary>Move one news report into a TV show (#1368). Same body as Move to TV show for a podcast episode.</summary>
public record NewsReportTvShowMoveCommand(Guid ReportId, EpisodeTvShowMoveRequest Request);
