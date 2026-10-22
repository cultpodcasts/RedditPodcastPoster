using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeTrailerAttachHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTrailerAttachCommand command,
        CancellationToken cancellationToken);
}
