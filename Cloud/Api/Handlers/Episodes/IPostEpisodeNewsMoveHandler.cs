using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeNewsMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeNewsMoveCommand command,
        CancellationToken cancellationToken);
}
