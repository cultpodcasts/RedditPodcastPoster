using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Episodes;

public interface IGetAwaitingApprovalEpisodesHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CancellationToken c);
}
