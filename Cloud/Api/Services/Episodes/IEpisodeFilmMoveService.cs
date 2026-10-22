using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeFilmMoveService
{
    Task<EpisodeFilmMoveResult> MoveAsync(EpisodeFilmMoveCommand command, CancellationToken cancellationToken);
}
