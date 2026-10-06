using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.DiscoverySchedule;

namespace Api.Handlers.DiscoverySchedule;

public class PutDiscoveryScheduleHandler(
    IDiscoveryScheduleUpdateService discoveryScheduleUpdateService,
    ILogger<PutDiscoveryScheduleHandler> logger) : IPutDiscoveryScheduleHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        DiscoveryScheduleUpdateRequest body,
        CancellationToken c)
    {
        var result = await discoveryScheduleUpdateService.UpdateAsync(body, c);
        return result.Status switch
        {
            DiscoveryScheduleUpdateStatus.Ok =>
                ctx.Accepted(),
            DiscoveryScheduleUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { error = result.Error }, c),
            DiscoveryScheduleUpdateStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Discovery schedule update failed with unexpected status.");
        return ctx.InternalError();
    }
}
