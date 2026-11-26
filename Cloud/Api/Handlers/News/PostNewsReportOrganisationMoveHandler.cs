using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

/// <summary>
/// POST newsorganisation/{sourceId}/reports/move. A command outcome, not the read model: 202 on success, 404 / 409 with
/// the per-id lists when the request was refused (nothing written), 400 for an invalid body, and 500 with
/// <c>failureIndexing</c> when the move did not finish (source reports kept).
/// </summary>
public class PostNewsReportOrganisationMoveHandler(INewsReportOrganisationMoveService moveService)
    : IPostNewsReportOrganisationMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        NewsReportOrganisationMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await moveService.MoveAsync(command, cancellationToken);
        return result.Status switch
        {
            NewsReportOrganisationMoveStatus.Accepted => await ctx.Accepted(result.ToDto(), cancellationToken),
            NewsReportOrganisationMoveStatus.NotFound => await ctx.NotFound(result.ToDto(), cancellationToken),
            NewsReportOrganisationMoveStatus.Conflict => await ctx.Conflict(result.ToDto(), cancellationToken),
            NewsReportOrganisationMoveStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            _ => await ctx.InternalError(result.ToDto(), cancellationToken)
        };
    }
}
