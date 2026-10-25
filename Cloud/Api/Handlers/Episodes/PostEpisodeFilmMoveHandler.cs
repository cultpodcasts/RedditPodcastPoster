using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeFilmMoveHandler(
    IEpisodeFilmMoveService episodeFilmMoveService,
    ILogger<PostEpisodeFilmMoveHandler> logger) : IPostEpisodeFilmMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeFilmMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await episodeFilmMoveService.MoveAsync(command, cancellationToken);
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
                await ctx.NotFound(new { podcastId = command.PodcastId, episodeId = command.EpisodeId }, cancellationToken),
            EpisodeFilmMoveStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeFilmMoveStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeFilmMoveStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to move episode to film"), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Episode to film move failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to move episode to film"), cancellationToken);
    }
}
