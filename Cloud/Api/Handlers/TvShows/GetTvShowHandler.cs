using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class GetTvShowHandler(
    ITvShowGetService tvShowGetService,
    ILogger<GetTvShowHandler> logger) : IGetTvShowHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string identifier,
        CancellationToken c)
    {
        var result = await tvShowGetService.GetAsync(identifier, c);

        return result.Status switch
        {
            TvShowGetStatus.Found =>
                await ctx.Ok(result.TvShow!.ToDto(), c),
            TvShowGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve TV show"), c),
            TvShowGetStatus.Conflict when result.AmbiguousIds != null =>
                await ctx.Conflict(result.AmbiguousIds, c),
            TvShowGetStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure("Unable to retrieve TV show"), c),
            TvShowGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV show"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("TV show get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV show"), c);
    }
}
