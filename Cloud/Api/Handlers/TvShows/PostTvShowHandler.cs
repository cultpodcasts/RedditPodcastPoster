using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.TvShows;

namespace Api.Handlers.TvShows;

public class PostTvShowHandler(
    ITvShowService tvShowService,
    ILogger<PostTvShowHandler> logger) : IPostTvShowHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TvShowChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await tvShowService.UpdateAsync(request, c);

        return result.Status switch
        {
            TvShowUpdateStatus.Accepted =>
                ctx.Accepted(),
            TvShowUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = request.TvShowId }, c),
            TvShowUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            TvShowUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update TV show"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("TV show update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update TV show"), c);
    }
}
