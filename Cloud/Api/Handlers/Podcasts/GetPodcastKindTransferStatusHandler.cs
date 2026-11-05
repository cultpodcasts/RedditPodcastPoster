using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class GetPodcastKindTransferStatusHandler(
    IPodcastKindTransferStatusService statusService,
    ILogger<GetPodcastKindTransferStatusHandler> logger) : IGetPodcastKindTransferStatusHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid podcastId,
        CancellationToken cancellationToken)
    {
        var result = await statusService.GetAsync(podcastId, cancellationToken);
        return result.Status switch
        {
            PodcastKindTransferJobQueryStatus.NotFound =>
                await ctx.NotFound(new { id = podcastId }, cancellationToken),
            PodcastKindTransferJobQueryStatus.Idle or
                PodcastKindTransferJobQueryStatus.Queued or
                PodcastKindTransferJobQueryStatus.Running or
                PodcastKindTransferJobQueryStatus.Completed or
                PodcastKindTransferJobQueryStatus.Failed or
                PodcastKindTransferJobQueryStatus.Incomplete =>
                await ctx.Ok(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Podcast kind transfer status query failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to read podcast kind transfer status"),
            cancellationToken);
    }
}
