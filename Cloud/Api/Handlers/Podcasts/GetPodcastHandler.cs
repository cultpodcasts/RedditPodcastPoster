using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class GetPodcastHandler(
    IPodcastGetService podcastGetService,
    ILogger<GetPodcastHandler> logger) : IGetPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastGetRequest podcastGetRequest,
        CancellationToken c)
    {
        var result = await podcastGetService.GetAsync(podcastGetRequest, c);

        return result.Status switch
        {
            PodcastGetStatus.Found =>
                await ctx.Ok(result.Podcast!.ToDto(), c),
            PodcastGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve podcast"), c),
            PodcastGetStatus.Conflict when result.AmbiguousPodcasts != null =>
                await ctx.Conflict(result.AmbiguousPodcasts, c),
            PodcastGetStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure("Unable to retrieve podcast"), c),
            PodcastGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve podcast"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Podcast get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve podcast"), c);
    }
}
