using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

public class PostEpisodeTrailerAttachHandler(
    IEpisodeTrailerAttachService episodeTrailerAttachService,
    ILogger<PostEpisodeTrailerAttachHandler> logger) : IPostEpisodeTrailerAttachHandler
{
    private const string FailureMessage = "Unable to attach episode as a trailer";

    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeTrailerAttachCommand command,
        CancellationToken cancellationToken)
    {
        var result = await episodeTrailerAttachService.AttachAsync(command, cancellationToken);
        return result.Status switch
        {
            EpisodeTrailerAttachStatus.Accepted =>
                await ctx.Accepted(result.ToDto(), cancellationToken),
            EpisodeTrailerAttachStatus.NotFound =>
                await ctx.NotFound(
                    new
                    {
                        podcastId = command.PodcastId,
                        episodeId = command.EpisodeId,
                        filmId = command.Request.FilmId,
                        error = result.Error
                    },
                    cancellationToken),
            EpisodeTrailerAttachStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            EpisodeTrailerAttachStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure(FailureMessage), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Episode trailer attach failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure(FailureMessage), cancellationToken);
    }
}
