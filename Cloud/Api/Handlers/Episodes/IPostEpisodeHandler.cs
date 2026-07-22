using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken c);
}
