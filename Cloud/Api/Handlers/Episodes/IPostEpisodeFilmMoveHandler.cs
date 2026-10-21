using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeFilmMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeFilmMoveCommand command,
        CancellationToken cancellationToken);
}
