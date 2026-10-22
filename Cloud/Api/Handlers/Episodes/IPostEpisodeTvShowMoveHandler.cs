using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeTvShowMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTvShowMoveCommand command,
        CancellationToken cancellationToken);
}
