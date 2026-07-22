using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.SearchIndex;

namespace Api.Handlers.SearchIndex;

public class RunSearchIndexHandler(
    ISearchIndexRunService searchIndexRunService,
    ILogger<RunSearchIndexHandler> logger) : IRunSearchIndexHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var result = await searchIndexRunService.RunAsync(c);
        return result.Status switch
        {
            SearchIndexRunStatus.Ok =>
                await ctx.Ok(result.Result!.ToDto(), c),
            SearchIndexRunStatus.BadRequest =>
                await ctx.BadRequest(result.Result!.ToDto(), c),
            SearchIndexRunStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update podcast"), c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Search index run failed with unexpected status.");
        return ctx.InternalError();
    }
}
