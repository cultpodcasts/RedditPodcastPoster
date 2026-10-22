using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeTvShowMoveService
{
    Task<EpisodeTvShowMoveResult> MoveAsync(EpisodeTvShowMoveCommand command, CancellationToken cancellationToken);
}
