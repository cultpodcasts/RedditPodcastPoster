using System.Net;
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

        // The transfer ignores cancellation once it writes, so it may finish after the caller has gone. A reply
        // that cannot be written then is expected, not an error: the outcome is already stored.
        if (cancellationToken.IsCancellationRequested)
        {
            return CallerGone(ctx, result);
        }

        try
        {
            return await Reply(ctx, result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CallerGone(ctx, result);
        }
    }

    private async Task<HttpResponseData> Reply(
        IHandlerContext ctx,
        PodcastKindTransferResult result,
        CancellationToken cancellationToken) =>
        result.Status switch
        {
            PodcastKindTransferStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            PodcastKindTransferStatus.NotFound =>
                await ctx.NotFound(new { id = result.ParentId }, cancellationToken),
            PodcastKindTransferStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            PodcastKindTransferStatus.AlreadyRunning or PodcastKindTransferStatus.Incomplete =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            PodcastKindTransferStatus.InvalidTarget =>
                await ctx.BadRequest(
                    ApiErrorResponse.Failure("targetKind must be TvShow or NewsOrganisation"),
                    cancellationToken),
            PodcastKindTransferStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to transfer podcast"), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };

    private HttpResponseData CallerGone(IHandlerContext ctx, PodcastKindTransferResult result)
    {
        logger.LogInformation(
            "Caller disconnected before the podcast kind transfer reply was written. Outcome for '{parentId}': " +
            "{status} to {targetKind} ({playableCount} playables).",
            result.ParentId,
            result.Status,
            result.TargetKind,
            result.PlayableCount);
        return ctx.Status(result.Status switch
        {
            PodcastKindTransferStatus.Accepted => HttpStatusCode.Accepted,
            PodcastKindTransferStatus.NotFound => HttpStatusCode.NotFound,
            PodcastKindTransferStatus.Conflict or PodcastKindTransferStatus.AlreadyRunning
                or PodcastKindTransferStatus.Incomplete => HttpStatusCode.Conflict,
            PodcastKindTransferStatus.InvalidTarget => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        });
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Podcast kind transfer failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to transfer podcast"), cancellationToken);
    }
}
