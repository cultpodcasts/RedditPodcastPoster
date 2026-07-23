using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Discovery;

namespace Api.Handlers.Discovery;

public class PostDiscoveryCurationHandler(
    IDiscoveryCurationSubmitService discoveryCurationSubmitService,
    ILogger<PostDiscoveryCurationHandler> logger) : IPostDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        DiscoverySubmitRequest model,
        CancellationToken c)
    {
        var result = await discoveryCurationSubmitService.SubmitAsync(model, c);

        return result.Status switch
        {
            DiscoveryCurationSubmitStatus.Ok =>
                await ctx.Ok(result.Outcome!.ToDto(), c),
            DiscoveryCurationSubmitStatus.Failed =>
                await ctx.InternalError(new { Message = "Failure" }, c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Discovery curation submit failed with unexpected status.");
        return await ctx.InternalError(new { Message = "Failure" }, c);
    }
}
