using System.Net;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Handlers.Catalogue;

/// <summary>
/// Maps a TV show / news organisation index to HTTP the way <c>IndexPodcastHandler</c> does: 200 with the indexed item
/// ids and search state (<c>enrichOnly</c> when only Spotify / Apple links were filled in), 404 not found, 400 not
/// indexable (<c>Removed</c>, <c>NotAutoIndexed</c>, <c>SourcePodcastStillExists</c>) or <c>NothingToEnrich</c> (body
/// says why), 503 <c>EnrichFailed</c> (enrich-only lookups did not finish; retryable), 409 ambiguous name (body is
/// the ids), 504
/// <c>{ reason: "TimedOut" }</c> when the on-demand time budget cut it off (not a fault, so not a 500), and 500 when
/// the index failed.
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
                    SearchIndexerState = result.SearchIndexer?.ToDto() ?? SearchIndexerState.Unknown,
                    EnrichOnly = result.EnrichOnly
                }, c);
            case CatalogueIndexStatus.NotFound:
                return ctx.NotFound();
            case CatalogueIndexStatus.Conflict when result.AmbiguousIds != null:
                return await ctx.Conflict(result.AmbiguousIds, c);
            case CatalogueIndexStatus.Conflict:
                return ctx.Conflict();
            case CatalogueIndexStatus.NotIndexable
                when result.NotIndexableReason == CatalogueParentNotIndexableReason.Removed:
                return await ctx.BadRequest(new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.Removed,
                    Message = CatalogueIndexRejection.RemovedMessage
                }, c);
            case CatalogueIndexStatus.NotIndexable:
                return await ctx.BadRequest(new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.NotAutoIndexed,
                    Message = CatalogueIndexRejection.NotAutoIndexedMessage
                }, c);
            case CatalogueIndexStatus.NothingToEnrich:
                return await ctx.BadRequest(new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.NothingToEnrich,
                    Message = CatalogueIndexRejection.NothingToEnrichMessage
                }, c);
            case CatalogueIndexStatus.EnrichFailed:
                return await ctx.Json(HttpStatusCode.ServiceUnavailable, new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.EnrichFailed,
                    Message = CatalogueIndexRejection.EnrichFailedMessage
                }, c);
            case CatalogueIndexStatus.SourcePodcastStillExists:
                return await ctx.BadRequest(
                    new CatalogueIndexRejection { Reason = CatalogueIndexRejection.SourcePodcastStillExists }, c);
            case CatalogueIndexStatus.TimedOut:
                return await ctx.Json(HttpStatusCode.GatewayTimeout, new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.TimedOut,
                    Message = CatalogueIndexRejection.TimedOutMessage
                }, c);
            case CatalogueIndexStatus.AlreadyRunning:
                return await ctx.Conflict(new CatalogueIndexRejection
                {
                    Reason = CatalogueIndexRejection.IndexingInProgress,
                    Message = CatalogueIndexRejection.IndexingInProgressMessage
                }, c);
            case CatalogueIndexStatus.Failed:
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to index {kind}"), c);
            default:
                logger.LogError("{kind} index failed with unexpected status.", kind);
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to index {kind}"), c);
        }
    }
}
