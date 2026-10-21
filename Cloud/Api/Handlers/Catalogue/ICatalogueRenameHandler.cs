using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Catalogue;

public interface ICatalogueRenameHandler
{
    Task<HttpResponseData> HandleNewsOrganisation(IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c);

    Task<HttpResponseData> HandleTvShow(IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c);

    Task<HttpResponseData> HandleFilm(IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c);
}
