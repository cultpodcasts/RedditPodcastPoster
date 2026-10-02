using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class PostTvShowEpisodeHandler(
    ITvShowEpisodeUpdateService tvShowEpisodeUpdateService,
    ILogger<PostTvShowEpisodeHandler> logger) : IPostTvShowEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await tvShowEpisodeUpdateService.UpdateAsync(request, c);

        return result.Status switch
        {
            TvShowUpdateStatus.Accepted =>
                ctx.Accepted(),
            TvShowUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = request.EpisodeId }, c),
            TvShowUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            TvShowUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update TV-show episode"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("TV-show episode update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update TV-show episode"), c);
    }
}
