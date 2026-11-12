using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.News;

public interface IDeleteNewsOrganisationHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Guid newsOrganisationId, CancellationToken c);
}
