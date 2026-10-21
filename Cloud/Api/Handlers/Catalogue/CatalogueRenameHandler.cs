using Api.Dtos;
using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api.Handlers.Catalogue;

/// <summary>Maps a rename result to HTTP the way <c>RenamePodcastHandler</c> does.</summary>
public class CatalogueRenameHandler(
    ICatalogueRenameService renameService,
    ILogger<CatalogueRenameHandler> logger) : ICatalogueRenameHandler
{
    public async Task<HttpResponseData> HandleNewsOrganisation(
        IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c) =>
        await Respond(ctx, await renameService.RenameNewsOrganisation(command, c), "news organisation", c);

    public async Task<HttpResponseData> HandleTvShow(
        IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c) =>
        await Respond(ctx, await renameService.RenameTvShow(command, c), "TV show", c);

    public async Task<HttpResponseData> HandleFilm(
        IHandlerContext ctx, CatalogueRenameCommand command, CancellationToken c) =>
        await Respond(ctx, await renameService.RenameFilm(command, c), "film", c);

    private async Task<HttpResponseData> Respond(
        IHandlerContext ctx, CatalogueRenameResult result, string kind, CancellationToken c)
    {
        switch (result.Status)
        {
            case CatalogueRenameStatus.Ok:
                return await ctx.Ok(CatalogueRenameResponse.From(result.SearchIndexer!), c);
            case CatalogueRenameStatus.Conflict when result.AmbiguousIds != null:
                return await ctx.Conflict(result.AmbiguousIds, c);
            case CatalogueRenameStatus.Conflict:
                return ctx.Conflict();
            case CatalogueRenameStatus.NotFound:
                return ctx.NotFound();
            case CatalogueRenameStatus.BadRequest:
                return ctx.BadRequest();
            case CatalogueRenameStatus.InvalidName:
                return ctx.InternalError();
            case CatalogueRenameStatus.Failed:
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to rename {kind}"), c);
            default:
                logger.LogError("{kind} rename failed with unexpected status.", kind);
                return await ctx.InternalError(ApiErrorResponse.Failure($"Unable to rename {kind}"), c);
        }
    }
}
