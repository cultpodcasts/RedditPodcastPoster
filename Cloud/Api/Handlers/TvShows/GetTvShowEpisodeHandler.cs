using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class GetTvShowEpisodeHandler(
    ITvShowEpisodeService tvShowEpisodeService,
    ILogger<GetTvShowEpisodeHandler> logger) : IGetTvShowEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid episodeId,
        CancellationToken c)
    {
        var result = await tvShowEpisodeService.GetAsync(episodeId, c);

        return result.Status switch
        {
            TvShowEpisodeGetStatus.Found =>
                await ctx.Ok(result.Episode!.ToDto(), c),
            TvShowEpisodeGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c),
            TvShowEpisodeGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("TV-show episode get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve TV-show episode"), c);
    }
}
