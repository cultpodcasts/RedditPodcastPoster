using Api.Dtos;
using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Catalogue;

public interface ICatalogueIdentityLookupHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CatalogueIdentityLookupRequest request,
        CancellationToken c);
}
