using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.News;

public interface IGetNewsOrganisationHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, string identifier, CancellationToken c);
}
