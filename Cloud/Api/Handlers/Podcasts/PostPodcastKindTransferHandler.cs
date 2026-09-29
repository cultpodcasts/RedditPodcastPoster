using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Handlers.Podcasts;

public class PostPodcastKindTransferHandler(
    IPodcastKindTransferService podcastKindTransferService,
    ILogger<PostPodcastKindTransferHandler> logger) : IPostPodcastKindTransferHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastKindTransferCommand command,
        CancellationToken cancellationToken)
    {
        var result = await podcastKindTransferService.TransferAsync(
            command.PodcastId,
            command.Request,
            cancellationToken);

        return result.Status switch
        {
            PodcastKindTransferStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            PodcastKindTransferStatus.NotFound =>
                await ctx.NotFound(new { id = result.ParentId }, cancellationToken),
            PodcastKindTransferStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            PodcastKindTransferStatus.InvalidTarget =>
                await ctx.BadRequest(
                    ApiErrorResponse.Failure("targetKind must be TvShow or NewsOrganisation"),
                    cancellationToken),
            PodcastKindTransferStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to transfer podcast"), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Podcast kind transfer failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to transfer podcast"), cancellationToken);
    }
}
