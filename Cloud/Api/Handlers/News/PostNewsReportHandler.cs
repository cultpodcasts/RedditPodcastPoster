using Api.Dtos.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class PostNewsReportHandler(
    INewsReportService newsReportService,
    ILogger<PostNewsReportHandler> logger) : IPostNewsReportHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportChangeRequestWrapper request,
        CancellationToken c)
    {
        var result = await newsReportService.UpdateAsync(request, c);

        return result.Status switch
        {
            NewsReportUpdateStatus.Accepted =>
                await ctx.Accepted((result.Outcome ?? new EpisodeUpdateOutcome()).ToDto(), c),
            NewsReportUpdateStatus.NotFound =>
                await ctx.NotFound(new { id = request.ReportId }, c),
            NewsReportUpdateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, c),
            NewsReportUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update news report"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("News report update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update news report"), c);
    }
}
