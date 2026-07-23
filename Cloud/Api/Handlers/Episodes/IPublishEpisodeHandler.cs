using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPublishEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken c);
}
