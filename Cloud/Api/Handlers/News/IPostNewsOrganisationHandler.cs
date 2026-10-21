using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.News;

public interface IPostNewsOrganisationHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsOrganisationChangeRequestWrapper request,
        CancellationToken c);
}
