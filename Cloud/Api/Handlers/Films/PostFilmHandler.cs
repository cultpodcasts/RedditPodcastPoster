using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.Films;

namespace Api.Handlers.Films;

public class PostFilmHandler(
    IFilmService filmService,
    ILogger<PostFilmHandler> logger) : IPostFilmHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        FilmChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await filmService.UpdateAsync(request, c);

        return result.Status switch
        {
            FilmUpdateStatus.Accepted =>
                ctx.Accepted(),
            FilmUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = request.FilmId }, c),
            FilmUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            FilmUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update film"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Film update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update film"), c);
    }
}
