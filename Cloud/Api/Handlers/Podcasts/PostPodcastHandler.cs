using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class PostPodcastHandler(
    IPodcastUpdateService podcastUpdateService,
    ILogger<PostPodcastHandler> logger) : IPostPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        CancellationToken c)
    {
        var result = await podcastUpdateService.UpdateAsync(podcastChangeRequestWrapper, c);

        return result.Status switch
        {
            PodcastUpdateStatus.Accepted when result.FailureIndexingEpisodes =>
                await ctx.Accepted(new { failureIndexingEpisodes = true }, c),
            PodcastUpdateStatus.Accepted when result.FailureDeletingFromIndex =>
                await ctx.Accepted(new { failureDeletingFromIndex = true }, c),
            PodcastUpdateStatus.Accepted =>
                ctx.Accepted(),
            PodcastUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = result.PodcastId }, c),
            PodcastUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update podcast"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Podcast update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update podcast"), c);
    }
}
