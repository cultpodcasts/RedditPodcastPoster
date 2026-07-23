using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.Discovery;

namespace Api.Handlers.Discovery;

public class GetDiscoveryCurationHandler(
    IDiscoveryCurationGetService discoveryCurationGetService,
    ILogger<GetDiscoveryCurationHandler> logger) : IGetDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var includeHidden = bool.TryParse(ctx.Query("includeHidden"), out var parsed) && parsed;
        var result = await discoveryCurationGetService.GetAsync(includeHidden, c);

        return result.Status switch
        {
            DiscoveryCurationGetStatus.Ok =>
                await ctx.Ok(result.Data!.ToDto(), c),
            DiscoveryCurationGetStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Discovery curation get failed with unexpected status.");
        return ctx.InternalError();
    }
}
