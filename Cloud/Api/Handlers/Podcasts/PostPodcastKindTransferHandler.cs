using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class PostPodcastKindTransferHandler(
    IPodcastKindTransferEnqueueService enqueueService,
    ILogger<PostPodcastKindTransferHandler> logger) : IPostPodcastKindTransferHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastKindTransferCommand command,
        CancellationToken cancellationToken)
    {
        var result = await enqueueService.EnqueueAsync(
            command.PodcastId,
            command.Request,
            cancellationToken);

        return result.Status switch
        {
            PodcastKindTransferEnqueueStatus.Queued =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            PodcastKindTransferEnqueueStatus.NotFound =>
                await ctx.NotFound(new { id = result.ParentId }, cancellationToken),
            PodcastKindTransferEnqueueStatus.AlreadyRunning =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            PodcastKindTransferEnqueueStatus.InvalidTarget =>
                await ctx.BadRequest(
                    ApiErrorResponse.Failure("targetKind must be TvShow or NewsOrganisation"),
                    cancellationToken),
            PodcastKindTransferEnqueueStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to enqueue podcast kind transfer"),
                    cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Podcast kind transfer enqueue failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to enqueue podcast kind transfer"),
            cancellationToken);
    }
}
