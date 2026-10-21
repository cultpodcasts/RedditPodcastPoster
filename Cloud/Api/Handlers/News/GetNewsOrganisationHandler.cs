using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class GetNewsOrganisationHandler(
    INewsOrganisationService newsOrganisationService,
    ILogger<GetNewsOrganisationHandler> logger) : IGetNewsOrganisationHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string identifier,
        CancellationToken c)
    {
        var result = await newsOrganisationService.GetAsync(identifier, c);

        return result.Status switch
        {
            NewsOrganisationGetStatus.Found =>
                await ctx.Ok(result.NewsOrganisation!.ToDto(), c),
            NewsOrganisationGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve news organisation"), c),
            NewsOrganisationGetStatus.Conflict when result.AmbiguousIds != null =>
                await ctx.Conflict(result.AmbiguousIds, c),
            NewsOrganisationGetStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure("Unable to retrieve news organisation"), c),
            NewsOrganisationGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news organisation"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("News organisation get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news organisation"), c);
    }
}
