using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class DeleteTvShowEpisodeHandler(
    ITvShowEpisodeService tvShowEpisodeService,
    ILogger<DeleteTvShowEpisodeHandler> logger) : IDeleteTvShowEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid episodeId,
        CancellationToken c)
    {
        var result = await tvShowEpisodeService.DeleteAsync(episodeId, c);

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
                        message = "Cannot remove TV-show episode.",
                        tweeted = result.Tweeted
                    }, c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("TV-show episode delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
