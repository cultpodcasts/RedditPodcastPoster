using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.Catalogue;

/// <summary>
/// Maps a TV show / news organisation index to HTTP the way <c>IndexPodcastHandler</c> does: 200 with the indexed item
/// ids and search state, 404 not found, 400 not indexable (body says why), 409 ambiguous name (body is the ids), 500
/// when the index failed or ran out of time.
/// </summary>
public class CatalogueIndexHandler(
    ICatalogueParentIndexService indexService,
    ILogger<CatalogueIndexHandler> logger) : ICatalogueIndexHandler
{
    public async Task<HttpResponseData> HandleTvShow(IHandlerContext ctx, string identifier, CancellationToken c) =>
        await Respond(ctx, await indexService.IndexTvShow(identifier, c), "TV show", c);

    public async Task<HttpResponseData> HandleNewsOrganisation(
        IHandlerContext ctx, string identifier, CancellationToken c) =>
        await Respond(ctx, await indexService.IndexNewsOrganisation(identifier, c), "news organisation", c);

    private async Task<HttpResponseData> Respond(
        IHandlerContext ctx, CatalogueIndexResult result, string kind, CancellationToken c)
    {
        switch (result.Status)
        {
            case CatalogueIndexStatus.Ok:
                return await ctx.Ok(new CatalogueIndexResponse
                {
                    IndexedItemIds = result.IndexedIds?.ToArray() ?? [],
                    SearchIndexerState = result.SearchIndexer?.ToDto() ?? SearchIndexerState.Unknown
                }, c);
            case CatalogueIndexStatus.NotFound:
                return ctx.NotFound();
            case CatalogueIndexStatus.Conflict when result.AmbiguousIds != null:
                return await ctx.Conflict(result.AmbiguousIds, c);
            case CatalogueIndexStatus.Conflict:
                return ctx.Conflict();
            case CatalogueIndexStatus.NotIndexable:
                return await ctx.BadRequest(
                    new CatalogueIndexRejection { Reason = CatalogueIndexRejection.NotAutoIndexed }, c);
            case CatalogueIndexStatus.SourcePodcastStillExists:
                return await ctx.BadRequest(
                    new CatalogueIndexRejection { Reason = CatalogueIndexRejection.SourcePodcastStillExists }, c);
            case CatalogueIndexStatus.BudgetExceeded:
                return await ctx.InternalError(
                    ApiErrorResponse.Failure($"Indexing the {kind} ran out of time; anything found before the cut was saved"), c);
            case CatalogueIndexStatus.Failed:
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to index {kind}"), c);
            default:
                logger.LogError("{kind} index failed with unexpected status.", kind);
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to index {kind}"), c);
        }
    }
}
