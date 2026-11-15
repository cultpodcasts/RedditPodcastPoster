using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;

namespace Api.Handlers.Episodes;

/// <summary>
/// Status mapping shared by the single and batch move-to-news routes. A command outcome, not the read model: 202 on
/// success, 404 / 409 with the per-id lists when the request was refused (nothing written), 400 for an invalid body,
/// and 500 with <c>failureIndexing</c> when the move did not finish (source episodes kept).
/// </summary>
public static class EpisodeNewsMoveResponses
{
    public static Task<HttpResponseData> Respond(
        IHandlerContext ctx,
        EpisodeNewsMoveResult result,
        CancellationToken cancellationToken) =>
        result.Status switch
        {
            EpisodeNewsMoveStatus.Accepted => ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeNewsMoveStatus.NotFound => ctx.NotFound(result.ToDto(), cancellationToken),
            EpisodeNewsMoveStatus.Conflict => ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeNewsMoveStatus.BadRequest =>
                ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            _ => ctx.InternalError(result.ToDto(), cancellationToken)
        };
}
