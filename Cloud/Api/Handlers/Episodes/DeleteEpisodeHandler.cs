using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class DeleteEpisodeHandler(
    IEpisodeDeleteService episodeDeleteService,
    ILogger<DeleteEpisodeHandler> logger) : IDeleteEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        CancellationToken c)
    {
        var result = await episodeDeleteService.DeleteAsync(podcastEpisodeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeDeleteStatus.Deleted =>
                ctx.Ok(),
            EpisodeDeleteStatus.PodcastConflict =>
                ctx.Conflict(),
            EpisodeDeleteStatus.NotFound =>
                ctx.NotFound(),
            EpisodeDeleteStatus.AlreadySocial =>
                await ctx.BadRequest(
                    new
                    {
                        message = "Cannot remove episode.",
                        posted = result.Posted,
                        tweeted = result.Tweeted
                    }, c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Episode delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
