using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class DeleteNewsReportHandler(
    INewsReportService newsReportService,
    ILogger<DeleteNewsReportHandler> logger) : IDeleteNewsReportHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid reportId,
        CancellationToken c)
    {
        var result = await newsReportService.DeleteAsync(reportId, c);

        return result.Status switch
        {
            EpisodeDeleteStatus.Deleted =>
                ctx.Ok(),
            EpisodeDeleteStatus.NotFound =>
                ctx.NotFound(),
            EpisodeDeleteStatus.AlreadySocial =>
                await ctx.BadRequest(
                    new
                    {
                        message = "Cannot remove news report.",
                        tweeted = result.Tweeted
                    }, c),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("News report delete failed with unexpected status.");
        return ctx.InternalError();
    }
}
