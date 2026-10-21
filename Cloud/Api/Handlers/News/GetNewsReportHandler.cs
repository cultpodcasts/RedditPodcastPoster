using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class GetNewsReportHandler(
    INewsReportService newsReportService,
    ILogger<GetNewsReportHandler> logger) : IGetNewsReportHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid reportId,
        CancellationToken c)
    {
        var result = await newsReportService.GetAsync(reportId, c);

        return result.Status switch
        {
            NewsReportGetStatus.Found =>
                await ctx.Ok(result.Report!.ToDto(), c),
            NewsReportGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve news report"), c),
            NewsReportGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news report"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("News report get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news report"), c);
    }
}
