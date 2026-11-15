using Api.Models;

namespace Api.Services.News;

public interface INewsReportTvShowMoveService
{
    Task<EpisodeTvShowMoveResult> MoveAsync(NewsReportTvShowMoveCommand command, CancellationToken cancellationToken);
}
