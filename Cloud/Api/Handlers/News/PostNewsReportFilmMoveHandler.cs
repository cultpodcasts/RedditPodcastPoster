using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

/// <summary>Same status mapping and response body as Move to film for a podcast episode (#1368).</summary>
public class PostNewsReportFilmMoveHandler(
    INewsReportFilmMoveService newsReportFilmMoveService,
    ILogger<PostNewsReportFilmMoveHandler> logger) : IPostNewsReportFilmMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportFilmMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await newsReportFilmMoveService.MoveAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeFilmMoveStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            // Same as URL-submit: ambiguous / cancel are 200 command outcomes, nothing written.
            EpisodeFilmMoveStatus.IdentityDecisionRequired =>
                await ctx.Ok(result.ToDto(), cancellationToken),
            EpisodeFilmMoveStatus.Cancelled =>
                await ctx.Ok(result.ToDto(), cancellationToken),
            EpisodeFilmMoveStatus.NotFound =>
                await ctx.NotFound(new { reportId = command.ReportId, error = result.Error }, cancellationToken),
            EpisodeFilmMoveStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeFilmMoveStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeFilmMoveStatus.Failed =>
                // Include ToDto so failureIndexing reaches the client.
                await ctx.InternalError(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("News report to film move failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to move news report to film"), cancellationToken);
    }
}
