using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.Discovery;

namespace Api.Handlers.Discovery;

public interface IPostDiscoveryChannelBanHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Guid resultId, CancellationToken cancellationToken);
}

public class PostDiscoveryChannelBanHandler(
    IDiscoveryChannelBanService discoveryChannelBanService,
    ILogger<PostDiscoveryChannelBanHandler> logger) : IPostDiscoveryChannelBanHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, Guid resultId, CancellationToken cancellationToken)
    {
        var result = await discoveryChannelBanService.BanAsync(resultId, cancellationToken);
        return result.Status switch
        {
            DiscoveryChannelBanStatus.Accepted => ctx.Accepted(),
            DiscoveryChannelBanStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure(result.Error ?? "Discovery result was not found."), cancellationToken),
            DiscoveryChannelBanStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Cannot remove this channel."), cancellationToken),
            DiscoveryChannelBanStatus.Conflict =>
                await ctx.Conflict(ApiErrorResponse.Failure(result.Error ?? "Cannot remove this channel."), cancellationToken),
            DiscoveryChannelBanStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure(result.Error ?? "Unable to remove the podcast."), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Discovery channel ban failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to remove the podcast."), cancellationToken);
    }
}
