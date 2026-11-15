using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

/// <summary>Same status mapping and response body as Move to TV show for a podcast episode (#1368).</summary>
public class PostNewsReportTvShowMoveHandler(
    INewsReportTvShowMoveService newsReportTvShowMoveService,
    ILogger<PostNewsReportTvShowMoveHandler> logger) : IPostNewsReportTvShowMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportTvShowMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await newsReportTvShowMoveService.MoveAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeTvShowMoveStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeTvShowMoveStatus.NotFound =>
                await ctx.NotFound(new
                {
                    reportId = command.ReportId,
                    tvShowId = command.Request.TvShowId,
                    error = result.Error
                }, cancellationToken),
            EpisodeTvShowMoveStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeTvShowMoveStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeTvShowMoveStatus.Failed =>
                // Same shape as the episode moves: 500 body carries failureIndexing.
                await ctx.InternalError(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("News report to TV show move failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to move news report to TV show"), cancellationToken);
    }
}
