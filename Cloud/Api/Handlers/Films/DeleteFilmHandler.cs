using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.Films;

namespace Api.Handlers.Films;

public class DeleteFilmHandler(
    IFilmService filmService,
    ILogger<DeleteFilmHandler> logger) : IDeleteFilmHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid filmId,
        CancellationToken c)
    {
        var result = await filmService.DeleteAsync(filmId, c);

        return result.Status switch
        {
            EpisodeDeleteStatus.Deleted =>
                ctx.Ok(),
            EpisodeDeleteStatus.NotFound =>
                ctx.NotFound(),
            EpisodeDeleteStatus.AlreadySocial =>
                await ctx.BadRequest(
                    new
                    {
                        message = "Cannot remove film.",
                        tweeted = result.Tweeted
                    }, c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Film delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
