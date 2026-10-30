using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Catalogue;

public interface ICatalogueIndexHandler
{
    Task<HttpResponseData> HandleTvShow(IHandlerContext ctx, string identifier, CancellationToken c);

    Task<HttpResponseData> HandleNewsOrganisation(IHandlerContext ctx, string identifier, CancellationToken c);
}
