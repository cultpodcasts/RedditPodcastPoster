using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.DiscoverySchedule;

namespace Api.Handlers.DiscoverySchedule;

public class GetDiscoveryScheduleHandler(
    IDiscoveryScheduleGetService discoveryScheduleGetService,
    ILogger<GetDiscoveryScheduleHandler> logger) : IGetDiscoveryScheduleHandler
{
    private const int NextRunsPreviewCount = 6;

    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await discoveryScheduleGetService.GetAsync(c);
        return result.Status switch
        {
            DiscoveryScheduleGetStatus.Ok =>
                await ctx.Ok(
                    DiscoveryScheduleResponseBuilder.Build(
                        result.Config!,
                        result.IsDefault,
                        NextRunsPreviewCount),
                    c),
            DiscoveryScheduleGetStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Discovery schedule get failed with unexpected status.");
        return ctx.InternalError();
    }
}
