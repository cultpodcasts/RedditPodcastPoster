using Api.Dtos;
using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.Catalogue;

public class CatalogueIdentityLookupHandler(
    ICatalogueIdentityLookupService lookupService,
    ILogger<CatalogueIdentityLookupHandler> logger) : ICatalogueIdentityLookupHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CatalogueIdentityLookupRequest request,
        CancellationToken c)
    {
        var result = await lookupService.LookupAsync(request, c);

        return result.Status switch
        {
            CatalogueIdentityLookupServiceStatus.Ok when result.Response is not null =>
                await ctx.Ok(result.Response, c),
            CatalogueIdentityLookupServiceStatus.BadRequest =>
                ctx.BadRequest(),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Catalogue identity lookup failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to look up catalogue identity"), c);
    }
}
