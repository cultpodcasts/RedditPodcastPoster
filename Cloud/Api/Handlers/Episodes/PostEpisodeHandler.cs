using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeHandler(
    IEpisodeUpdateService episodeUpdateService,
    ILogger<PostEpisodeHandler> logger) : IPostEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken c)
    {
        var result = await episodeUpdateService.UpdateAsync(episodeChangeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeUpdateStatus.Accepted =>
                await ctx.Accepted(result.Outcome!.ToDto(), c),
            EpisodeUpdateStatus.NotFound =>
                ctx.NotFound(),
            EpisodeUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update episode"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Episode update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update episode"), c);
    }
}
