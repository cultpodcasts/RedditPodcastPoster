using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Discovery;

namespace Api.Handlers.Discovery;

public class PostDiscoveryCurationHandler(
    IDiscoveryCurationSubmitService discoveryCurationSubmitService,
    IDiscoverySubmissionHoldService discoverySubmissionHoldService,
    ILogger<PostDiscoveryCurationHandler> logger) : IPostDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Api.Models.DiscoverySubmitRequest model,
        CancellationToken c)
    {
        if (ctx.HasScope(DiscoveryPermissions.CurateSupervised))
        {
            return await Hold(ctx, model, c);
        }

        var result = await discoveryCurationSubmitService.SubmitAsync(model, c);

        return result.Status switch
        {
            DiscoveryCurationSubmitStatus.Ok =>
                await ctx.Ok(result.Outcome!.ToDto(), c),
            DiscoveryCurationSubmitStatus.Conflict =>
                await ctx.Conflict(new
                {
                    Message = "One or more reports are not available for curation.",
                    DocumentIds = result.UnavailableDocumentIds ?? []
                }, c),
            DiscoveryCurationSubmitStatus.Failed =>
                await ctx.InternalError(new { Message = "Failure" }, c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> Hold(
        IHandlerContext ctx,
        Api.Models.DiscoverySubmitRequest model,
        CancellationToken c)
    {
        if (string.IsNullOrWhiteSpace(ctx.Subject))
        {
            logger.LogError("Supervised discovery submit has no subject claim.");
            return await ctx.BadRequest(new { Message = "Missing subject." }, c);
        }

        var held = await discoverySubmissionHoldService.HoldAsync(model, ctx.Subject, c);
        return held.Status switch
        {
            DiscoverySubmissionHoldStatus.Held => await ctx.Accepted(
                new DiscoverySubmissionHeldResponse
                {
                    SubmissionId = held.SubmissionId!.Value,
                    Status = "Pending"
                }, c),
            DiscoverySubmissionHoldStatus.Conflict => await ctx.Conflict(
                new { Message = "One or more reports are not available for curation.", DocumentIds = held.UnavailableDocumentIds ?? [] }, c),
            DiscoverySubmissionHoldStatus.InvalidResultIds => await ctx.BadRequest(
                new { Message = "Result ids are not in the submitted reports.", ResultIds = held.UnknownResultIds ?? [] }, c),
            _ => await ctx.InternalError(new { Message = "Failure" }, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Discovery curation submit failed with unexpected status.");
        return await ctx.InternalError(new { Message = "Failure" }, c);
    }
}
