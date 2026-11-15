using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostPodcastNewsMoveHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastNewsMoveCommand command,
        CancellationToken cancellationToken);
}
