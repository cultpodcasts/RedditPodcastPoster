using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeTvShowTrailerAttachHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTvShowTrailerAttachCommand command,
        CancellationToken cancellationToken);
}

