using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Episodes;

public interface IGetOutgoingEpisodesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CancellationToken c);
}
