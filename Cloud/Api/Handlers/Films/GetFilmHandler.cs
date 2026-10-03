using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Films;

namespace Api.Handlers.Films;

public class GetFilmHandler(
    IFilmService filmService,
    ILogger<GetFilmHandler> logger) : IGetFilmHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string identifier,
        CancellationToken c)
    {
        var result = await filmService.GetAsync(identifier, c);

        return result.Status switch
        {
            FilmGetStatus.Found =>
                await ctx.Ok(result.Film!.ToDto(), c),
            FilmGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve film"), c),
            FilmGetStatus.Conflict when result.AmbiguousIds != null =>
                await ctx.Conflict(result.AmbiguousIds, c),
            FilmGetStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure("Unable to retrieve film"), c),
            FilmGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve film"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Film get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve film"), c);
    }
}
