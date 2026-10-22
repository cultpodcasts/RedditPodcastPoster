using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeTvShowMoveHandler(
    IEpisodeTvShowMoveService episodeTvShowMoveService,
    ILogger<PostEpisodeTvShowMoveHandler> logger) : IPostEpisodeTvShowMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTvShowMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await episodeTvShowMoveService.MoveAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeTvShowMoveStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeTvShowMoveStatus.NotFound =>
                await ctx.NotFound(new
                {
                    podcastId = command.PodcastId,
                    episodeId = command.EpisodeId,
                    tvShowId = command.Request.TvShowId,
                    error = result.Error
                }, cancellationToken),
            EpisodeTvShowMoveStatus.Conflict =>
                await ctx.Conflict(result.ToDto(), cancellationToken),
            EpisodeTvShowMoveStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeTvShowMoveStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to move episode to TV show"), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Episode to TV show move failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to move episode to TV show"), cancellationToken);
    }
}
