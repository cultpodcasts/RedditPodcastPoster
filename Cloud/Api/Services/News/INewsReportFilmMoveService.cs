using Api.Models;

namespace Api.Services.News;

public interface INewsReportFilmMoveService
{
    Task<EpisodeFilmMoveResult> MoveAsync(NewsReportFilmMoveCommand command, CancellationToken cancellationToken);
}
